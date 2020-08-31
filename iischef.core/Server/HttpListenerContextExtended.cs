using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;

namespace iischef.core.Server
{
    public class HttpListenerContextExtended
    {
        private Hashtable data = new Hashtable();

        public HttpListenerContext CTX;

        public HttpListenerContextExtended(HttpListenerContext ctx)
        {
            this.CTX = ctx;
        }

        /// <summary>
        /// Send server event to client.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <param name="ctx"></param>
        public void SendServerEvent(string id, string data, bool flush = false)
        {
            this.WriteToStream(string.Format("id: {0}" + Environment.NewLine, id));
            this.WriteToStream(string.Format("data: {0}" + Environment.NewLine, data));
            this.WriteToStream(Environment.NewLine);

            if (flush)
            {
                this.CTX.Response.OutputStream.Flush();
            }
        }

        /// <summary>
        /// Write the string to the response stream.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="ctx"></param>
        public void WriteToStream(string data)
        {
            byte[] buf = Encoding.UTF8.GetBytes(data);
            this.CTX.Response.OutputStream.Write(buf, 0, buf.Length);
        }

        public void ReturnAccessDenied()
        {
            this.CTX.Response.StatusCode = 403;
        }

        private bool ResponseSent = false;

        private void ResponseOnlySentOnce()
        {
            if (this.ResponseSent)
            {
                throw new Exception("Can only send response once.");
            }

            this.ResponseSent = true;
        }

        public void ReturnObject(object result)
        {
            this.ResponseOnlySentOnce();

            var response = new Response();
            response.result = result;

            var data = Newtonsoft.Json.JsonConvert.SerializeObject(response);
            this.WriteToStream(data);
        }

        public void ReturnException(Exception ex, bool clear_output = false)
        {
            this.ResponseOnlySentOnce();

            var response = new Response();
            response.result = null;
            response.unhandled_exception = ex.Message;

            var data = Newtonsoft.Json.JsonConvert.SerializeObject(response);
            this.WriteToStream(data);
        }

        public HttpListenerRequest Request
        {
            get
            {
                return this.CTX.Request;
            }
        }

        public HttpListenerResponse Response
        {
            get
            {
                return this.CTX.Response;
            }
        }

        public string GetRequestIPidentifier()
        {
            string proxy = string.Empty;

            // Proxy
            if (this.CTX.Request.Headers["X-Forwarded-For"] != null)
            {
                proxy = this.CTX.Request.Headers["X-Forwarded-For"];
            }

            // Proxy
            if (this.CTX.Request.Headers["X-Client-IP"] != null)
            {
                proxy = this.CTX.Request.Headers["X-Client-IP"];
            }

            // Unique ID is combination of Proxy and original source address.
            return proxy + ":" + this.CTX.Request.RemoteEndPoint.Address.ToString();
        }

        public System.Collections.Specialized.NameValueCollection GetInput()
        {
            if (!this.data.ContainsKey("GetData"))
            {
                // Read the iniput (for example POST data)
                var request = this.CTX.Request;
                string text;
                using (var reader = new StreamReader(
                    request.InputStream,
                    request.ContentEncoding))
                {
                    text = reader.ReadToEnd();
                    this.data["GetData"] = System.Web.HttpUtility.ParseQueryString(text);
                }
            }

            return (System.Collections.Specialized.NameValueCollection)this.data["GetData"];
        }
    }
}
