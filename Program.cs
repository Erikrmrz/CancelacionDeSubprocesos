using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace CancelacionDeSubprocesos
{
    internal class Cancelacion
    {
        public static void Main()
        {
            // Ejemplo 1: Cancelación básica con ThreadPool
            CancellationTokenSource cts = new CancellationTokenSource();
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoSomeWork), cts.Token);
            Thread.Sleep(2500);
            cts.Cancel();
            Console.WriteLine("Cancelacion iniciada en el token");
            Thread.Sleep(2500);
            cts.Dispose();

            // Ejemplo 2: Cancelación en bucles anidados
            CancellationTokenSource cts2 = new CancellationTokenSource();
            Task.Run(() =>
            {
                Example1.NestedLoops(new Rectangle(0, 0, 5, 3), cts2.Token);
            });
            Thread.Sleep(100);
            cts2.Cancel();
            Thread.Sleep(500);

            // Ejemplo 3: Cancelación de petición web simulada
            CancellationTokenSource cts3 = new CancellationTokenSource();
            Task.Run(() => example4.StartWebRequest(cts3.Token));
            Thread.Sleep(100);
            cts3.Cancel();
            Thread.Sleep(500);

            // Ejemplo 4: Cancelación combinada (timeout + usuario)
            var worker = new example4();
            CancellationTokenSource timeoutCts = new CancellationTokenSource(200); // Timeout de 200ms
            CancellationTokenSource userCts = new CancellationTokenSource();
            Task.Run(() => worker.DoWork(userCts.Token));
            Thread.Sleep(100);
            userCts.Cancel();
            Thread.Sleep(500);

            Console.WriteLine("Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }

        //Listener
        static void DoSomeWork(object? obj)
        {
            if (obj is null)
                return;
            CancellationToken token = (CancellationToken)obj;
            for (int i = 0; i < 100000; i++)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("En la iteracion {0}, " +
                        "Se ha pedido la cancelacion...",
                    i + 1);
                    break;
                }
                // Simulate some work.
                Thread.SpinWait(500000);
            }
        }
    }
    class CancelableObject
    {
        public string id;
        public CancelableObject(string id)
        {
            this.id = id;
        }
        public void Cancel()
        {
            Console.WriteLine($"Object {id} Cancel callback");
        }
    }
    public class Example1
    {
        public static void NestedLoops(Rectangle rect, CancellationToken token)
        {
            for (int col = 0; col < rect.Height && !token.IsCancellationRequested; col++)
            {
                for (int row = 0; row < rect.Width; row++)
                {
                    Thread.SpinWait(5_000);
                    Console.Write("{0},{1} ", col, row);
                }
            }
            if (token.IsCancellationRequested)
            {
                // Cleanup or undo here if necessary...
                Console.WriteLine("\r\nOperation canceled");
                Console.WriteLine("Press any key to exit.");
                // If using Task:
                // token.ThrowIfCancellationRequested();
            }
        }
    }
    //REGISTRO DE DEVOLUCION DE LLAMADA
    class example4
    {
        private CancellationToken internalToken; // Added field for internalToken
        private CancellationToken externalToken; // Added field for externalToken

        public static void StartWebRequest(CancellationToken token)
        {
            var client = new HttpClient();
            token.Register(() =>
            {
                client.CancelPendingRequests();
                Console.WriteLine("Request cancelled!");
            });
            Console.WriteLine("Starting request.");
            client.GetStringAsync(new Uri("http://www.contoso.com"));
        }

        public void DoWork(CancellationToken externalToken)
        {
            this.externalToken = externalToken; // Assign externalToken to the class field
            using (CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(internalToken, externalToken))
            {
                try
                {
                    DoWorkInternal(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (internalToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Operation timed out.");
                    }
                    else if (externalToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Cancelling per user request.");
                        externalToken.ThrowIfCancellationRequested();
                    }
                }
            }
        }

        private void DoWorkInternal(CancellationToken token)
        {
            for (int i = 0; i < 10; i++)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("DoWorkInternal: Cancelado en la iteración " + i);
                    token.ThrowIfCancellationRequested();
                }
                Thread.Sleep(50);
            }
            Console.WriteLine("DoWorkInternal: Trabajo completado.");
        }
    }
}
