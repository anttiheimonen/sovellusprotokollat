using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;


// Pop3Service odottaa määritettyyn porttiin saapuvia yhteyksiä. Kun asiakas
// ottaa yhtettä, niin luokka käynnistää Pop3-yhteyden käsittelyn uuden 
// uudessa threadissa. 
class Pop3Service
{
    static bool running = false;
    static bool lopetettu = false;
    static Mailbox mailbox;

    public void run(int port, Mailbox userMailbox)
    {

        mailbox = userMailbox;
        running = true;
        TcpListener socket = null;
        try
        {
            socket = new TcpListener(IPAddress.Loopback, port);
            socket.Start();

            while (running && !lopetettu)
            {
                if (socket.Pending())
                {
                    TcpClient client = socket.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(handleClientConnection, client);
                }
                Thread.Sleep(1);
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
        }
        finally
        {
            Console.WriteLine("Pop3 loptettu");
            socket.Stop();
        }
    }


    public static bool LaunchServer(WaitCallback callBack, TcpClient client)
    {
        ThreadPool.QueueUserWorkItem(callBack, client);
        return true;
    }

    private static void handleClientConnection(object obj)
    {
        TcpClient client = null;
        NetworkStream ns = null;

        try
        {
            client = (TcpClient)obj;
            ns = client.GetStream();
            StreamReader sr = new StreamReader(ns);
            StreamWriter sw = new StreamWriter(ns);

            String req = "";
            bool on = true;

            Pop3Process p = new Pop3Process(mailbox);
            send(sw, "+OK POP3 Palvelin valmiina");

            while (on && !lopetettu)
            {
                req = sr.ReadLine();
                Console.WriteLine("Saatu POP3 pyyntö: {0}", req);

                String[] res = p.proceed(req);
                send(sw, res);
                if (res[0].Equals("+OK logged off"))
                    on = false;
            }

        }
        catch (System.Exception)
        {
            throw;
        }
        finally
        {
            Console.WriteLine("POP3 yhteys poikki");
            ns.Close();
            client.Close();
        }
    }


    static void send(StreamWriter sw, string[] messages)
    {
        for (int i = 0; i < messages.Length; i++)
        {
            send(sw, messages[i]);
        }
    }


    static void send(StreamWriter sw, string viesti)
    {
        sw.WriteLine(viesti);
        sw.Flush();
        Console.WriteLine("Lähetetty: " + viesti);
    }


    public static void quit()
    {
        lopetettu = true;
    }
}


// Luokka käsittelee pop3-protokollan tiedonvaihtamista. Pop3Process toimii
// palvelinpuolena, eli ottaa vastaan protokollan mukaisia pyyntöjä. 
// Luokka pitää myös yllä pop3 prosessin tilaa.
class Pop3Process
{

    private string user = string.Empty;
    private string password;
    private Pop3State state;
    private Mailbox mailbox;


    public Pop3Process(Mailbox userMailbox)
    {
        state = Pop3State.Authorization;
        mailbox = userMailbox;
        user = string.Empty;
        password = string.Empty;
    }


    public string[] proceed(string request)
    {
        Request req = new Request(request);

        if (!req.IsValid)
        {
            req.setResponse("+ERR unknown command");
            return req.Response;
        }

        if (req.Command.Equals("QUIT"))
        {
            req.setResponse("+OK logged off");
        }


        switch (state)
        {
            case Pop3State.Authorization:
                authorizationStateHandler(req);
                break;

            case Pop3State.Transaction:
                transactionStateHandler(req);
                break;
            default:
                break;
        }

        return req.Response;
    }


    // Käsittelijä pop3 processin authorization tilalle
    private void authorizationStateHandler(Request req)
    {
        switch (req.Command)
        {
            case "USER":
                user = req.Parameter;
                req.setResponse("+OK");
                break;

            case "PASS":
                if (user == string.Empty)
                {
                    req.setResponse("-ERR Give username first");
                }
                else
                {
                    password = req.Parameter;
                }

                if (user.Length > 0 && password.Length > 0)
                {
                    // Kun käyttäjätunnus ja salasana on annettu, niin tilaksi
                    // muuttuu Pop3State.Transaction.
                    state = Pop3State.Transaction;
                    System.Console.WriteLine("kirjauduttu");
                    req.setResponse("+OK logged in");
                }
                break;

            default:
                req.setResponse("+ERR unknown command");
                break;
        }
    }


    // Käsittelijä pop3 processin transaction tilalle
    private void transactionStateHandler(Request req)
    {
        switch (req.Command)
        {
            case "LIST":
                list(req);
                break;

            case "RETR":
                break;

            case "QUIT":

                break;

            default:
                req.setResponse("+ERR Unknown command");
                break;
        }
    }


    // Parametrittoman LIST käskyn käsittelijä
    private void list(Request req)
    {
        string[] lines = new string[mailbox.NumberOfMessages + 2];
        lines[0] = "+OK " + mailbox.NumberOfMessages + " messages";
        string[] mailList = mailbox.getMailList();
        for (int i = 1; i < lines.Length - 1; i++)
        {
            lines[i] = mailList[i - 1];
            System.Console.WriteLine("List rivi " + lines[i]);
        }

        lines[mailbox.NumberOfMessages + 1] = ".";
        req.Response = lines;
    }


    public (string, string) getCommandAndParameter(string req)
    {
        System.Console.WriteLine("req pituus {0}", req.Length);

        // Otetaan commandiksi 4 ensimmaista merkkia
        string command = req.Substring(0, 4).ToUpper();

        System.Console.WriteLine("POP3 pyynto {0}", command);

        string parameter = string.Empty;
        // Tarkista onko pyynnössä mukana parametrejä
        if (req.Length > 5)
            parameter = req.Substring(5, req.Length - 5);

        System.Console.WriteLine("POP3 parameter {0}", parameter);

        return (command, parameter);
    }


    enum Pop3State
    {
        Authorization,
        Transaction
    }
}


class Request
{

    private string request;
    private string _command;
    private bool _isValidRequest = true;
    private bool _hasParameter = false;
    private string _parameter;
    private string[] _response;


    public Request(string request)
    {
        this.request = request;
        getCommandAndParameter(request);
    }


    private void getCommandAndParameter(string req)
    {
        if (req.Length < 4)
        {
            this._isValidRequest = false;
            return;
        }

        System.Console.WriteLine("req pituus {0}", req.Length);

        // Otetaan commandiksi 4 ensimmaista merkkia
        _command = req.Substring(0, 4).ToUpper();

        System.Console.WriteLine("POP3 pyynto {0}", _command);

        _parameter = string.Empty;

        // Tarkista onko pyynnössä mukana parametrejä
        if (req.Length > 5)
        {
            this._parameter = req.Substring(5, req.Length - 5);
            _hasParameter = true;
        }
        else
        {
            _parameter = string.Empty;
            _hasParameter = false;
        }

        System.Console.WriteLine("POP3 parameter {0}", _parameter);
    }


    public string Command
    {
        get => _command;
    }


    public string[] Response
    {
        get => _response;
        set => _response = value;
    }


    public void setResponse(string res)
    {
        Response = new string[] { res };
    }


    public void setResponse(string[] res)
    {
        Response = res;
    }


    public string Parameter
    {
        get => _parameter;
    }


    public bool HasParameter
    {
        get => _hasParameter;
    }


    public bool IsValid
    {
        get => _isValidRequest;
    }
}

