using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;


class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("TFTP SERVER");
        TftpServer server = new TftpServer();
        server.run();
    }
}

class TftpServer
{
    Socket controlSocket = null;
    Socket dataSocket = null;
    int port = 69;
    private int Timeout = 5000;


    public void run()
    {
        try
        {
            byte[] request = new byte[512];
            IPEndPoint iep = new IPEndPoint(IPAddress.Any, port);
            controlSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            controlSocket.Bind(iep);

            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remote = (EndPoint)client;
            int received = controlSocket.ReceiveFrom(request, ref remote);

            String rec_string = Encoding.ASCII.GetString(request, 0, received);
            int clientport = ((IPEndPoint)remote).Port;

            byte[] rec2 = new byte[512];
            // Luo uusi soketti satunnaisesta portista tiedonsiirtoon
            var rand = new Random();
            int dataPort = rand.Next(5000, 60000);
            IPEndPoint iep2 = new IPEndPoint(IPAddress.Loopback, dataPort);
            dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            dataSocket.Bind(iep2);

            EndPoint ep = (EndPoint)remote;

            int reqType;
            string fileName;

            (reqType, fileName) = getRequestTypeAndFileName(request);

            dataSocket.ReceiveTimeout = Timeout;

            switch (reqType)
            {
                case 1:
                    sendFile(dataSocket, ep, fileName);
                    break;

                case 2:
                    receiveFile(dataSocket, ep, fileName);
                    break;

                default:
                    break;
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine("Virhe... " + ex.Message);
            Console.ReadKey();
            return;
        }
        finally
        {
            controlSocket.Close();
        }
    }


    private void receiveFile(Socket s, EndPoint remote, string fileName)
    {
        try
        {
            byte[] PacketToSend = Packet.ACKForWRQ();

            sendPacket(s, remote, PacketToSend);
            // s.SendTo(PacketToSend, ep);

            Packet p = new Packet();
            int blockNumber = 1;

            byte[] response = new byte[516];
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            remote = (EndPoint)client;

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
                        System.Console.WriteLine("Time out {0}", e);
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


    public void sendFile(Socket s, EndPoint ep, string fileName)
    {
        //    2 bytes     2 bytes      n bytes
        //    ----------------------------------
        //   | Opcode |   Block #  |   Data     |
        //    ----------------------------------
        // RFC: The data field is from zero to 512 bytes long

        int blockNumber = 1;
        byte[] packet;

        using (FileStream fs = File.OpenRead(fileName))
        {
            do
            {
                bool ACKReceived = false;
                int attempts = 0;

                packet = Packet.createDataPacket(fs, blockNumber);
                blockNumber++;
                do
                {
                    attempts++;

                    // Tahallinen virhe
                    // if (blockNumber == 200 && attempts < 3)
                    //     System.Console.WriteLine("Tahallinen virhe");
                    // else
                    sendPacket(s, ep, packet);

                    // Odota ACK-viestiä
                    ACKReceived = waitACKfor(packet, s, ep);
                } while (!ACKReceived && attempts < 5);

                if (attempts > 4)
                {
                    System.Console.WriteLine("Tiedostoa ei voitu lähettää");
                    break;
                }

            } while (packet.Length > 515);
        }
    }


    private (int, string) getRequestTypeAndFileName(byte[] req)
    {
        byte[] fileNameArr = new byte[512];
        string fileName;
        int fileNameLength = 0;

        for (int i = 2; i < req.Length; i++)
        {
            if (req[i] != 0)
            {
                fileNameArr[i - 2] = req[i];
                fileNameLength++;
            }
            else
            {
                break;
            }
        }

        fileName = Encoding.ASCII.GetString(fileNameArr, 0, fileNameLength);
        return (req[1], fileName);
    }


    public void sendPacket(Socket socket, EndPoint ep, byte[] packet)
    {
        socket.SendTo(packet, ep);
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


class Packet
{
    // RRQ 1
    // WRQ 2
    // DATA 3
    // ACK 4

    static public byte[] ACKForWRQ()
    {
        return new byte[] { 0, 4, 0, 0 };
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
