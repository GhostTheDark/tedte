using System;
using System.Threading.Tasks;
using RustlikeServer.Core;

namespace RustlikeServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Rust-Like Game Server";
            Console.ForegroundColor = ConsoleColor.Cyan;
            
            Console.WriteLine(@"
   ____           _     _     _ _          
  |  _ \ _   _ __| |_  | |   (_) | _____   
  | |_) | | | / _` __| | |   | | |/ / _ \  
  |  _ <| |_| \__ \ |_  | |___| |   <  __/  
  |_| \_\\__,_|___/\__| |_____|_|_|\_\___|  
                                            
              SERVIDOR DEDICADO
            ");
            
            Console.ResetColor();

            int port = 7777;
            
            if (args.Length > 0)
            {
                if (int.TryParse(args[0], out int customPort))
                {
                    port = customPort;
                }
            }

            GameServer server = new GameServer(port);

            // Ctrl+C handler
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n[Program] Encerrando servidor...");
                server.Stop();
                Environment.Exit(0);
            };

            await server.StartAsync();
        }
    }
}