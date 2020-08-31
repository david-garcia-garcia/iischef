using iischef.logger;
using System;
using System.Net;
using System.Threading;

namespace iischef.core.Server
{
    /// <summary>
    /// We have an embedded web server to receive control
    /// commands.
    /// </summary>
    public class Server
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly Action<HttpListenerContextExtended> responderMethod;

        protected SystemLogger logger;
        private readonly System.Timers.Timer cronTimer;

        public Server(string[] prefixes, SystemLogger logger, Action<HttpListenerContextExtended> method)
        {
            this.logger = logger;

            if (!HttpListener.IsSupported)
            {
                throw new NotSupportedException(
                    "Needs Windows XP SP2, Server 2003 or later.");
            }

            // URI prefixes are required, for example 
            // "http://localhost:8080/index/".
            if (prefixes == null || prefixes.Length == 0)
            {
                throw new ArgumentException("prefixes");
            }

            // A responder method is required
            if (method == null)
            {
                throw new ArgumentException("method");
            }

            foreach (string s in prefixes)
            {
                this.listener.Prefixes.Add(s);
            }

            this.responderMethod = method;
            this.listener.Start();

            this.cronTimer = new System.Timers.Timer();
            this.cronTimer.Elapsed += this.CronTimer_Elapsed;

            // Cron que se ejecuta cada hora.
            this.cronTimer = new System.Timers.Timer(1000 * 3600);
            this.cronTimer.Start();
        }

        private void CronTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.cronTimer.Stop();

            this.cronTimer.Start();
        }

        public Server(Action<HttpListenerContextExtended> method, SystemLogger logger, params string[] prefixes)
            : this(prefixes, logger, method)
        {
        }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                try
                {
                    while (this.listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem(
                            (c) =>
                        {
                            var ctx = new HttpListenerContextExtended(c as HttpListenerContext);
                            try
                            {
                                this.responderMethod(ctx);
                            }
                            catch (Exception e)
                            {
                                ctx.ReturnException(e, true);
                            }
                            finally
                            {
                                try
                                {
                                    // always close the stream
                                    ctx.Response.OutputStream.Close();
                                }
                                catch (Exception ex2)
                                {
                                    this.logger.LogError("Unhandled exception while closing response stream: " + ex2.Message);
                                }
                            }
                        }, this.listener.GetContext());
                    }
                }
                catch (Exception ex3)
                {
                    this.logger.LogError("Unhandled exception while running server loop: " + ex3.Message);
                }
            });
        }

        public void Stop()
        {
            this.cronTimer.Stop();
            this.listener.Stop();
            this.listener.Close();
        }
    }
}
