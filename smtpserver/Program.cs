using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        bool running = true;
        EmailServer emailServer = new EmailServer();

        Console.WriteLine("Argumentteja {0}", args.Length);

        try
        {
            ThreadPool.QueueUserWorkItem(startEmailServer, emailServer);

            string command;
            while (running)
            {
                command = Console.ReadLine();

                switch (command)
                {
                    case "quit":
                        stopEmailServer(emailServer);
                        running = false;
                        break;
                    case "info":
                        Console.WriteLine("Threads {0}", ThreadPool.ThreadCount);
                        break;
                    default:
                        Console.WriteLine("Tuntematon komento {0}", command);
                        break;
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
            Console.WriteLine("Loppu");
        }
    }


    public static void startEmailServer(object emaiServer)
    {
        EmailServer e = (EmailServer)emaiServer;
        Console.WriteLine("EMAIL SERVICE START");
        e.startServices();
    }


    static void stopEmailServer(EmailServer e)
    {
        e.quit();
    }
}


