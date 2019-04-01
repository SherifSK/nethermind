using System;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

using Nethermind.Core;
using Nethermind.Core.Logging;
using Nethermind.JsonRpc.Client;

namespace Nethermind.EthStats
{
    class EthStatsService : BasicJsonRpcClient
    {
        const int ReadSize = 256;
        private static object idLock = new object();
        private static int id = 0;

        public EthStatsService(Uri uri, IJsonSerializer jsonSerializer, ILogManager logManager) : base(
            uri, jsonSerializer, logManager)
        {

        }

        private Stream CopyAndClose(Stream inputStream)
        {
            
            byte[] buffer = new byte[ReadSize];
            MemoryStream ms = new MemoryStream();

            int count = inputStream.Read(buffer, 0, ReadSize);
            while (count > 0)
            {
                ms.Write(buffer, 0, count);
                count = inputStream.Read(buffer, 0, ReadSize);
            }

            ms.Position = 0;
            inputStream.Close();
            return ms;
        }

        public IObservable<JsonResponse<T>> Invoke<T>(string method, object arg, IScheduler scheduler)
        {
            var req = new JsonRequest()
            {
                Method = method,
                Params = new object[] { arg }
            };
            return Invoke<T>(req, scheduler);
        }

        public IObservable<JsonResponse<T>> Invoke<T>(string method, object[] args, IScheduler scheduler)
        {
            var req = new JsonRequest()
            {
                Method = method,
                Params = args
            };
            return Invoke<T>(req, scheduler);
        }

        public IObservable<JsonResponse<T>> Invoke<T>(JsonRequest jsonRpc, IScheduler scheduler)
        {
            var res = Observable.Create<JsonResponse<T>>((obs) =>
                scheduler.Schedule(() => {

                    WebRequest req = null;
                    try
                    {
                        int myId;
                        lock (idLock)
                        {
                            myId = ++id;
                        }
                        jsonRpc.Id = myId.ToString();
                        req = HttpWebRequest.Create(new Uri(ServiceEndpoint, "?callid=" + myId.ToString()));
                        req.Method = "Post";
                        req.ContentType = "application/json-rpc";
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }

                    var ar = req.BeginGetRequestStream(new AsyncCallback((iar) =>
                    {
                        HttpWebRequest request = null;

                        try
                        {
                            request = (HttpWebRequest)iar.AsyncState;
                            var stream = new StreamWriter(req.EndGetRequestStream(iar));
                            var json = Newtonsoft.Json.JsonConvert.SerializeObject(jsonRpc);
                            stream.Write(json);

                            stream.Close();
                        }
                        catch (Exception ex)
                        {
                            obs.OnError(ex);
                        }

                        var rar = req.BeginGetResponse(new AsyncCallback((riar) =>
                        {
                            JsonResponse<T> rjson = null;
                            string sstream = "";
                            try
                            {
                                var request1 = (HttpWebRequest)riar.AsyncState;
                                var resp = (HttpWebResponse)request1.EndGetResponse(riar);

                                using (var rstream = new StreamReader(CopyAndClose(resp.GetResponseStream())))
                                {
                                    sstream = rstream.ReadToEnd();
                                }

                                rjson = Newtonsoft.Json.JsonConvert.DeserializeObject<JsonResponse<T>>(sstream);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                Debugger.Break();
                            }

                            if (rjson == null)
                            {
                                if (!string.IsNullOrEmpty(sstream))
                                {
                                    JObject jo = Newtonsoft.Json.JsonConvert.DeserializeObject(sstream) as JObject;
                                    obs.OnError(new Exception(jo["Error"].ToString()));
                                }
                                else
                                {
                                    obs.OnError(new Exception("Empty response"));
                                }
                            }

                            obs.OnNext(rjson);
                            obs.OnCompleted();
                        }), request);
                    }), req);
                }));

            return res;
        }
    }
}
