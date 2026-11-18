using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class Server
{
    //lista in cui vengono memorizzati i giocatori
    static List<Socket> giocatori = new List<Socket>();
    static object blocco = new object();

    public static void Main(String[] args)
    {
        //creazione della socket
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //accetta tutti gli indirizzi ip sulla porta 5000
        listener.Bind(new IPEndPoint(IPAddress.Any, 5000));
        //massimo 10 client nella lista di attesa
        listener.Listen(10);
        Console.WriteLine("Server in ascolto sulla porta 5000...");

        while (true)
        {
            Socket handler = listener.Accept();

            //utilizzo di un semaforo per permettere al server di poter aggiornare la lista client senza che più thread lo modificano
            lock (blocco)
            {
                if (giocatori.Count >= 2)
                {
                    // Stanza piena: rifiuta
                    Invia(handler, "PIENO");
                    //chiude immediatamente la pagina
                    handler.Close();
                    continue;
                }

                // Se la lista è vuota, il primo client ha le pedine di colore Bianco, altrimenti di colore Nero
                string colore = giocatori.Count == 0 ? "B" : "N";
                //aggiunge il nuovo socket alla lista
                giocatori.Add(handler);
                //comunica al client appena connesso (handler) e gli dice il colore
                Invia(handler, colore);
                Console.WriteLine("Giocatore connesso, colore: " + colore);

                // Solo con 2 giocatori la partita può iniziare
                if (giocatori.Count == 2)
                {
                    foreach (Socket g in giocatori)
                        Invia(g, "START");
                    Console.WriteLine("Partita iniziata.");
                }
            }

            Thread t = new Thread(() => GestisciGiocatore(handler));
            t.Start();
        }
    }

    static void GestisciGiocatore(Socket s)
    {
        StringBuilder buffer = new StringBuilder();
        byte[] bytes = new byte[1024];
        bool finito = false;

        try
        {
            while (!finito)
            {
                int n = s.Receive(bytes);
                if (n == 0) break;

                buffer.Append(Encoding.ASCII.GetString(bytes, 0, n));

                int pos;
                while (!finito && (pos = buffer.ToString().IndexOf('$')) >= 0)
                {
                    string msg = buffer.ToString(0, pos);
                    buffer.Remove(0, pos + 1);
                    Console.WriteLine("Messaggio ricevuto : " + msg);

                    if (msg == "Quit")
                        finito = true;
                    else
                        Inoltra(s, msg);   // manda la mossa all'altro giocatore
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            Rimuovi(s);
        }
    }

    // Manda il messaggio a tutti tranne al mittente
    static void Inoltra(Socket mittente, string msg)
    {
        lock (blocco)
        {
            foreach (Socket g in giocatori)
            {
                if (g != mittente)
                    Invia(g, msg);
            }
        }
    }

    // Toglie il giocatore e avvisa l'altro che la partita è finita
    static void Rimuovi(Socket s)
    {
        lock (blocco)
        {
            giocatori.Remove(s);
            foreach (Socket g in giocatori)
                Invia(g, "Quit");
        }
        s.Close();
        Console.WriteLine("Giocatore disconnesso.");
    }

    static void Invia(Socket s, string msg)
    {
        try
        {
            s.Send(Encoding.ASCII.GetBytes(msg + "$"));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}