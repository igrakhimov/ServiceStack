using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace ServiceStack.Templates
{
    public class TemplateProtectedFilters : TemplateFilter
    {
        public async Task includeFile(TemplateScopeContext scope, string virtualPath)
        {
            var file = scope.Context.VirtualFiles.GetFile(virtualPath);
            if (file == null)
                throw new FileNotFoundException($"includeFile '{virtualPath}' in page '{scope.Page.File.VirtualPath}' was not found");

            using (var reader = file.OpenRead())
            {
                await reader.CopyToAsync(scope.OutputStream);
            }
        }

        public Task includeUrl(TemplateScopeContext scope, string url) => includeUrl(scope, url, null);
        public async Task includeUrl(TemplateScopeContext scope, string url, object options)
        {
            var scopedParams = scope.AssertOptions(nameof(includeUrl), options);

            var webReq = (HttpWebRequest)WebRequest.Create(url);
            var dataType = scopedParams.TryGetValue("dataType", out object value)
                ? ConvertDataTypeToContentType((string)value)
                : null;

            if (scopedParams.TryGetValue("method", out value))
                webReq.Method = (string)value;
            if (scopedParams.TryGetValue("contentType", out value) || dataType != null)
                webReq.ContentType = (string)value ?? dataType;            
            if (scopedParams.TryGetValue("accept", out value) || dataType != null) 
                webReq.Accept = (string)value ?? dataType;            
            if (scopedParams.TryGetValue("userAgent", out value))
                PclExport.Instance.SetUserAgent(webReq, (string)value);

            if (scopedParams.TryRemove("data", out object data))
            {
                if (webReq.Method == null)
                    webReq.Method = HttpMethods.Post;
                    
                if (webReq.ContentType == null)
                    webReq.ContentType = MimeTypes.FormUrlEncoded;

                var body = ConvertDataToString(data, webReq.ContentType);
                using (var stream = await webReq.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(body);
                }
            }

            using (var webRes = await webReq.GetResponseAsync())
            using (var stream = webRes.GetResponseStream())
            {
                await stream.CopyToAsync(scope.OutputStream);
            }
        }

        private static string ConvertDataTypeToContentType(string dataType)
        {
            switch (dataType)
            {
                case "json":
                    return MimeTypes.Json;
                case "jsv":
                    return MimeTypes.Jsv;
                case "csv":
                    return MimeTypes.Csv;
                case "xml":
                    return MimeTypes.Xml;
                case "text":
                    return MimeTypes.PlainText;
                case "formUrlEncoded":
                    return MimeTypes.FormUrlEncoded;
            }
            
            throw new NotSupportedException($"Unknown dataType '{dataType}'");
        }

        private static string ConvertDataToString(object data, string contentType)
        {
            if (data is string s)
                return s;
            switch (contentType)
            {
                case MimeTypes.PlainText:
                    return data.ToString();
                case MimeTypes.Json:
                    return data.ToJson();
                case MimeTypes.Csv:
                    return data.ToCsv();
                case MimeTypes.Jsv:
                    return data.ToJsv();
                case MimeTypes.Xml:
                    return data.ToXml();
                case MimeTypes.FormUrlEncoded:
                    WriteComplexTypeDelegate holdQsStrategy = QueryStringStrategy.FormUrlEncoded;
                    QueryStringSerializer.ComplexTypeStrategy = QueryStringStrategy.FormUrlEncoded;
                    var urlEncodedBody = QueryStringSerializer.SerializeToString(data);
                    QueryStringSerializer.ComplexTypeStrategy = holdQsStrategy;
                    return urlEncodedBody;
            }

            throw new NotSupportedException($"Can not serialize to unknown Content-Type '{contentType}'");
        }
    }
}