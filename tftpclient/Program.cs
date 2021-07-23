using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;

class Program
{
    static int portNumer = 69;
    static string server = "127.0.0.1";
    static string fileName = "alice.txt";


    static void Main(string[] args)
    {
        uploadFile();
        // deleteAndGet();
    }


    private static void deleteAndGet()
    {
        try
        {
            File.Delete(@fileName);
        }
        catch (System.Exception)
        {

            throw;
        }

        TftpClient client = new TftpClient();
        client.getFile(server, portNumer, fileName);
    }


    private static void uploadFile()
    {
        TftpClient client = new TftpClient();
        client.sendFile(server, portNumer, fileName);
    }
}

class TftpClient
{
    private const int Timeout = 5000; // Timeout aika vastauksen odottamiselle
    private const int MaxRetries = 5; // Kuinka monta kertaa paketti yritetään lähettää  


    public TftpClient()
    {

    }


    private (Socket, EndPoint) createSocket(string server, int port)
    {
        Socket s = new Socket(AddressFamily.InterNetwork,
                   SocketType.Dgram,
                   ProtocolType.Udp);

        IPAddress host = IPAddress.Parse(server);
        IPEndPoint iep = new IPEndPoint(host, port);
        EndPoint ep = (EndPoint)iep;
        return (s, ep);
    }


    public void getFile(string server, int port, string fileName)
    {
        Socket s = null;

        try
        {
            EndPoint ep = null;
            Packet p = new Packet();
            int blockNumber = 1;
            (s, ep) = createSocket(server, port);

            // Luo ja lähetä lukupyyntö
            byte[] PacketToSend = Packet.createRequest(fileName, "RRQ");
            s.SendTo(PacketToSend, ep);

            Console.WriteLine("RRQ lähetetty");

            byte[] response = new byte[516];
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remote = (EndPoint)client;

            s.ReceiveTimeout = Timeout;
            int bytesGot = 0;
            int sendAttempt = 0;
            int errorCount = 0;

            bool lastPacketReceived = false;
            using (FileStream fs = File.OpenWrite(fileName))
            {
                // Datan sisältävä packetti on kooltaan 4 + 512. 
                // Pienempi paketti tarkoittaa viimeistä data-pakettia.
                do
                {
                    // Silmukka lukee saadun datan socketista.
                    // Jos saapunut tftp-paketti on oikea data-paketti,
                    // niin PacketToSend-muuttujaan asetetaan uusi Ack-viesti.
                    // Muissa tapauksissa muuttujaan ei aseteta uutta arvoa,
                    // vaan sen sisältämä edellinen paketti lähetetään uudestaan.
                    try
                    {
                        bytesGot = s.ReceiveFrom(response, ref remote);

                        // Tarkistetaan, että paketti on oikea
                        if (Packet.dataPacketIsCorrect(response, blockNumber))
                        {
                            blockNumber++;
                            fs.Write(response, 4, bytesGot - 4);
                            sendAttempt = 0;
                            // Luo uusi ack-paketti
                            PacketToSend = Packet.createACKForPacket(response);
                            // Viimeinen paketti on kooltaan alle 516
                            if (bytesGot < 516)
                                lastPacketReceived = true;
                        }
                        else
                        {
                            System.Console.WriteLine("Virhe BlockNumberissa {0}", blockNumber);
                        }
                    }
                    catch (SocketException e)
                    {
                        // Time Out soketin lukemisessa
                        System.Console.WriteLine("Time out {0}", e);
                        errorCount++;
                    }

                    if (sendAttempt > MaxRetries)
                    {
                        System.Console.WriteLine("Oikeaa pakettia ei saatu {0} yrityksellä", MaxRetries);
                        break;
                    }

                    // ACK Lähetys
                    sendAttempt++;
                    s.SendTo(PacketToSend, remote);

                } while (!lastPacketReceived);
            }

            Console.WriteLine("Paketteja saatu: {0}", blockNumber);
            Console.WriteLine("Virheitä tapahtui: {0}", errorCount);
        }
        catch (System.Exception)
        {
            throw;
        }
        finally
        {
            s.Close();
        }
    }


    public void sendFile(string server, int port, string fileName)
    {
        Socket s;

        EndPoint ep = null;
        Packet p = new Packet();
        int blockNumber = 1;

        (s, ep) = createSocket(server, port);
        s.SendTo(Packet.createRequest(fileName, "WRQ"), ep);
        Console.WriteLine("WRQ lähetetty");

        byte[] response = new byte[516];
        IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remote = (EndPoint)client;
        int count = 0;

        count = s.ReceiveFrom(response, ref remote);

        if (response[1] == 4)
        {
            System.Console.WriteLine("Oikea vastaus");
        }
        byte[] packet;

        using (FileStream fs = File.OpenRead(fileName))
        {
            do
            {
                bool ACKReceived = false;
                int attempts = 0;

                packet = Packet.createDataPacket(fs, blockNumber);
                blockNumber++;

                // Lähettää paketin ja odottaa oikeaa ACK-viestiä. Mikäli
                // väärä viesti saapuu, niin lähetetään paketti uudelleen.
                do
                {
                    attempts++;
                    // Odota ACK-viestiä
                    s.SendTo(packet, remote);
                    ACKReceived = waitACKfor(packet, s, ep);
                } while (!ACKReceived || attempts > 5);

            } while (packet.Length > 515);
        }
    }


    // Metodi odottaa ACK-viestiä paketille. ACKia odotetaan määritetyn 
    // Timeout-ajan verran. 
    private bool waitACKfor(byte[] forPacket, Socket socket, EndPoint ep)
    {
        //           2 bytes    2 bytes
        //           -------------------
        //    ACK   | 04    |   Block #  |
        //           --------------------

        // Asetetaan sokettiin Timeout arvo.
        socket.ReceiveTimeout = Timeout;
        DateTime deadline = DateTime.Now.AddMilliseconds(Timeout);
        byte[] rec = new byte[512];
        try
        {
            while (true)
            {
                int received = 0;
                received = socket.ReceiveFrom(rec, ref ep);

                // ACK-koodin ja block-numeron tarkastaminen
                if (Packet.isACKFor(rec, forPacket))
                {
                    return true;
                }

                // Jos saatu paketti ei ollutkaan oikea ACK, niin lasketaan 
                // soketille uusi timeout-arvo vähentämällä oletus 
                // Timeout-arvosta odottamiseen jo käytetty aika.
                socket.ReceiveTimeout = (int)(deadline - DateTime.Now).TotalMilliseconds;
            }
        }
        catch (SocketException e)
        {
            System.Console.WriteLine("Time out {0}", e);
        }

        return false;
    }
}


public class Packet
{
    // Luo RRQ tai WRQ paketin, jolla client aloittaa yhteyden.
    static public byte[] createRequest(string filename, string type)
    {
        //  2 bytes     string    1 byte     string   1 byte
        //  ------------------------------------------------
        // | Opcode |  Filename  |   0  |    Mode    |   0  |
        //  ------------------------------------------------
        byte[] fnBytes = Encoding.ASCII.GetBytes(filename);
        byte[] mode = Encoding.ASCII.GetBytes("octet");
        byte[] arr = new byte[fnBytes.Length + mode.Length + 4];
        arr[0] = 0;

        switch (type)
        {
            case "RRQ":
                arr[1] = 1;
                break;

            case "WRQ":
                arr[1] = 2;
                break;

            default:
                break;
        }

        int n = 2;
        for (int i = 0; i < fnBytes.Length; i++)
        {
            arr[n] = fnBytes[i];
            n++;
        }

        arr[n] = 0;
        n++;

        for (int i = 0; i < mode.Length; i++)
        {
            arr[n] = mode[i];
            n++;
        }

        arr[n] = 0;
        return arr;
    }


    // Luo ACK-viestin annetulle paketille
    static public byte[] createACKForPacket(byte[] packet)
    {
        byte[] ack = new byte[4];
        ack[1] = 4;
        ack[2] = packet[2];
        ack[3] = packet[3];
        return ack;
    }


    // Tarkistaa onko AckPacket ACK-viesti forPacketille. 
    static public bool isACKFor(byte[] AckPacket, byte[] ForPacket)
    {
        if (AckPacket.Length < 4)
            return false;

        return (AckPacket[1] == 4 &&
                AckPacket[2] == ForPacket[2] &&
                AckPacket[3] == ForPacket[3]);
    }


    // Luo annetusta FileStreamista data-paketin
    static public byte[] createDataPacket(FileStream fileStream, int blockNumber)
    {
        // Luodaan byte-taulukot headerille ja datalle ja yhdistetääŋ
        // ne yhdeksi lähetettäväksi byte-taulukoksi.
        byte[] fileBuffer = new byte[512];
        // Headerin koko on 4 tavua
        byte[] header = new byte[4];
        header[1] = 3;
        header[2] = (byte)(blockNumber / 256);
        header[3] = (byte)(blockNumber % 256);

        int bytesReadFromFile = fileStream.Read(fileBuffer, 0, fileBuffer.Length);

        byte[] packet = new byte[4 + bytesReadFromFile];
        Buffer.BlockCopy(header, 0, packet, 0, 4);
        Buffer.BlockCopy(fileBuffer, 0, packet, 4, bytesReadFromFile);

        return packet;
    }


    // Tarkistaa, että saatu paketti on dataa ja sen block-numero on oikea
    static public bool dataPacketIsCorrect(byte[] packet, int blockNumber)
    {
        if (packet.Length < 4)
            return false;

        if (packet[1] != 3)
            return false;

        int ackFor = packet[2] * 256 + packet[3];
        return (ackFor == blockNumber);
    }
}
