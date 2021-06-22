using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        // try
        // {
        //     File.Delete(@"alice.txt");
        // }
        // catch (System.Exception)
        // {

        //     throw;
        // }
        TftpClient client = new TftpClient();
        // client.getFile("192.168.1.2", 9999, "alice.txt");

        // client.getFile("192.168.1.181", 69, "alice.txt");
        client.sendFile("192.168.1.2", 9999, "alice3.txt");
    }
}

class TftpClient
{

    private Socket s = null;
    private EndPoint remote = null;
    private const int Timeout = -1; // Timeout aika vastauksen odottamiselle
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
        try
        {
            EndPoint ep = null;
            Packet p = new Packet();
            int blockNumber = 1;

            (s, ep) = createSocket(server, port);
            // Lähetä lukupyyntö
            byte[] PacketToSend = Packet.createRequest(fileName, "RRQ");
            s.SendTo(PacketToSend, ep);

            Console.WriteLine("RRQ lähetetty");

            byte[] response = new byte[516];
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            remote = (EndPoint)client;

            s.ReceiveTimeout = Timeout;
            int bytesGot = 0;
            int sendAttempt = 0;

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

                        // Jos data-paketti on oikeanlainen
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
                            System.Console.WriteLine("Virhe BlockNumberissa");
                        }
                    }
                    catch (SocketException e)
                    {
                        // Time Out soketin lukemisessa
                        System.Console.WriteLine("Time out");
                    }

                    if (sendAttempt > 4)
                    {
                        System.Console.WriteLine("Oikeaa pakettia ei saatu 5 yrityksellä");
                        break;
                    }

                    // ACK Lähetys
                    sendAttempt++;
                    s.SendTo(PacketToSend, remote);

                } while (!lastPacketReceived);
            }

            System.Console.WriteLine("Paketteja saatu: {0}", blockNumber);
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
        EndPoint ep = null;
        Packet p = new Packet();
        int blockNumber = 1;

        (s, ep) = createSocket(server, port);
        s.SendTo(Packet.createRequest(fileName, "WRQ"), ep);
        Console.WriteLine("WRQ lähetetty");

        byte[] response = new byte[516];
        IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
        remote = (EndPoint)client;
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


    private bool waitACKfor(byte[] forPacket, Socket socket, EndPoint ep)
    {
        //           2 bytes    2 bytes
        //           -------------------
        //    ACK   | 04    |   Block #  |
        //           --------------------

        byte[] rec = new byte[512];
        try
        {
            int received = 0;
            received = socket.ReceiveFrom(rec, ref ep);
            if (received < 4)
            {
                // Liian pieni ACK paketiksi
                return false;
            }

            // ACK-koodin ja block-numeron tarkastaminen
            if (Packet.isACKFor(rec, forPacket))
            {
                return true;
            }

        }
        catch (SocketException e)
        {
            System.Console.WriteLine("Time out");
        }

        return false;
    }


    private void waitAck(Socket socket, EndPoint ep)
    {
        byte[] rec = new byte[512];
        int received = socket.ReceiveFrom(rec, ref ep);
    }
}


class Packet
{
    // Luo RRQ tai WRQ paketin
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


    static public bool isACKFor(byte[] AckPacket, byte[] AckForPacket)
    {
        return (AckPacket[1] == 4 && AckPacket[2] == AckForPacket[2] && AckPacket[3] == AckForPacket[3]);
    }


    // Tekee ACK-viestin annetulle paketille
    static public byte[] createACKForPacket(byte[] packet)
    {
        byte[] ack = new byte[4];
        ack[1] = 4;
        ack[2] = packet[2];
        ack[3] = packet[3];
        return ack;
    }


    // Tarkistaa, että saatu paketti on dataa ja sen bock-numero on oikea
    static public bool dataPacketIsCorrect(byte[] packet, int blockNumber)
    {
        if (packet[1] != 3)
        {
            return false;
        }

        int ackFor = packet[2] * 256 + packet[3];
        return (ackFor == blockNumber);
    }


    static public byte[] createDataPacket(FileStream fileStream, int blockNumber)
    {
        // Luodaan byte-taulukot headerille ja datalle ja yhdistetääŋ
        // ne yhdeksi lähetettäväksi byte arrayksi.
        // Silmukkaa toistetaan kunnes tiedosto on lopussa ja siitä
        // lukemalla saadaan alle 512 tavua.
        byte[] fileBuffer = new byte[512];
        // Headerin koko on 4 tavua
        byte[] header = new byte[4];
        header[1] = 3;

        header[2] = (byte)(blockNumber / 256);
        header[3] = (byte)(blockNumber % 256);
        blockNumber++;

        int bytesReadFromFile = fileStream.Read(fileBuffer, 0, fileBuffer.Length);

        byte[] packet = new byte[4 + bytesReadFromFile];
        Buffer.BlockCopy(header, 0, packet, 0, 4);
        Buffer.BlockCopy(fileBuffer, 0, packet, 4, bytesReadFromFile);

        return packet;
    }


    static public bool ACKIsCorrectFor(byte[] packet, int blockNumber)
    {
        if (packet[1] != 4)
        {
            return false;
        }

        int ackFor = packet[2] * 256 + packet[3];
        return (ackFor == blockNumber);
    }

}


