using System;
using System.IO;
using System.Net.Sockets;
using System.Text;


// FTP-asiakas ohjelma. Ohjelma osaa toteuttaa käskyt USER, PASS, EPSV,
// LIST, RETR ja QUIT. 
class Program
{
    static Socket controlSocket = null;
    static string username = "anonymous"; // string.Empty;
    static string password = ""; //string.Empty;
    static NetworkStream controlNS = null;
    static bool logged = false;
    static string serverAddress = string.Empty;

    static void Main(string[] args)
    {
        int FTPport = 21;
        serverAddress = "127.0.0.1";

        try
        {
            controlSocket = getSocket(serverAddress, FTPport);
            controlNS = new NetworkStream(controlSocket);
            readStream(controlNS);

            string res = "";

            while (!logged)
            {
                // Console.WriteLine("User name:");
                // username = System.Console.ReadLine();

                // Console.WriteLine("Password:");
                // password = System.Console.ReadLine();

                sendCommand(controlNS, "user " + username);
                res = readStream(controlNS);


                sendCommand(controlNS, "pass " + password);
                res = readStream(controlNS);

                if (res.StartsWith("230"))
                {
                    logged = true;
                    System.Console.WriteLine("Kirjauduttu");
                }
            }

            // System.Console.WriteLine(res);
            res = getFileList();
            System.Console.WriteLine(res);

            while (true)
            {
                System.Console.WriteLine("Anna ladattavan tiedoston nimi tai quit lopettaaksesi");
                string fileToDownload = System.Console.ReadLine();

                if (fileToDownload.Equals("quit"))
                {
                    break;
                }

                downloadFile(fileToDownload);
            }
            sendCommand(controlNS, "quit");
            readStream(controlNS);
        }
        catch (System.Exception)
        {
            throw;
        }
        finally
        {
            controlNS.Close();
            controlSocket.Close();
        }
    }


    // Palauttaa avoimen tcp-soketin 
    static private Socket getSocket(string address, int port)
    {
        Socket s = new Socket(AddressFamily.InterNetwork,
                           SocketType.Stream,
                           ProtocolType.Tcp);
        s.Connect(address, port);

        return s;
    }


    static string readStream(NetworkStream ns)
    {
        StringBuilder message = new StringBuilder();
        if (ns.CanRead)
        {
            byte[] readBuffer = new byte[1024];
            int numberOfBytesRead = 0;

            do
            {
                numberOfBytesRead = ns.Read(readBuffer, 0, readBuffer.Length);
                message.AppendFormat("{0}", Encoding.ASCII.GetString(readBuffer, 0, numberOfBytesRead));
                // System.Console.WriteLine(message);
            }
            while (ns.DataAvailable);
        }
        return message.ToString();
    }


    // Lähettää komennon NetworkStreamiin
    static private void sendCommand(NetworkStream ns, string command)
    {
        StreamWriter sw = new StreamWriter(ns);
        sw.WriteLine(command);
        sw.Flush();
    }


    // Palauttaa portti numeron epsv-komennon vastausviestistä.
    static int getPortFromEpsvMessage(string message)
    {
        int start = message.IndexOf("|||");
        int end = message.LastIndexOf('|');
        int length = end - (start + 3);

        string port = message.Substring(start + 3, length);

        int portnumber = 0;
        Int32.TryParse(port, out portnumber);
        return portnumber;
    }


    // Metodi lähettää espv pyynnön ja palauttaa avoimen socketin
    // tiedon siirtoa varten.
    static private Socket GetEPSVConnection()
    {
        string res = string.Empty;
        sendCommand(controlNS, "epsv");
        res = readStream(controlNS);
        int dataport = getPortFromEpsvMessage(res);
        Socket s = getSocket(serverAddress, dataport);

        return s;
    }


    // Palautta tiedostolistauksen.
    static private string getFileList()
    {
        Socket s = GetEPSVConnection();
        NetworkStream dataStream = new NetworkStream(s);
        sendCommand(controlNS, "list");
        string res = readStream(controlNS);
        res = readStream(dataStream);
        readStream(controlNS);
        return res;
    }


    // Lataa tiedoston palvelimelta epsv-moodissa
    static private string downloadFile(string fileName)
    {
        Socket s = null;
        NetworkStream dataStream = null;
        string res = String.Empty;
        try
        {
            s = GetEPSVConnection();
            dataStream = new NetworkStream(s);

            sendCommand(controlNS, "retr " + fileName);  // Pyytää tiedostoa
            res = readStream(controlNS);  // Odottaa koodia 150
            if (!getCodeFromResponse(res).Equals("150"))
                return "Ei voi ladata tiedostoa";

            receiveFileFromStream(dataStream, fileName);  // Tiedoston lataus datastreamillä
            res = readStream(controlNS); // Odottaa koodia 226
        }
        catch (System.Exception e)
        {
            System.Console.WriteLine(e);
        }
        finally
        {
            dataStream.Close();
            s.Close();
        }
        return "Tiedoston lataus onnistui";
    }


    // Lukee tiedoston NetworkStreamistä ja tallentaa tiedostoon
    static private void receiveFileFromStream(NetworkStream dataNS, string fileName)
    {
        byte[] readBuffer = new byte[1024];
        int numberOfBytesRead = 0;

        using (FileStream fs = File.OpenWrite(fileName))
        {
            do
            {
                numberOfBytesRead = dataNS.Read(readBuffer, 0, readBuffer.Length);
                fs.Write(readBuffer, 0, numberOfBytesRead);
            }
            while (dataNS.DataAvailable);
        }
    }


    static private string getCodeFromResponse(string response)
    {
        if (response.Length < 3)
            return "000";

        return response.Substring(0, 3);
    }

}

