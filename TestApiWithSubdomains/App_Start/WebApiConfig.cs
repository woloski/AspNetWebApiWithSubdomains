using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using System.Web.Http.Routing;

namespace TestApiWithSubdomains
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.Routes.Add("test", new HttpDomainRoute(
                domain: "{tenant}.auth10.com",
                routeTemplate: "test",
                defaults: new { controller = "Values", action = "GetTenant" }
            ));
        }
    }


    public class HttpDomainRoute
        : HttpRoute
    {
        private Regex domainRegex;
        private Regex pathRegex;

        public HttpDomainRoute(string domain, string routeTemplate, object defaults, object constraints = null)
            : base(routeTemplate, new HttpRouteValueDictionary(defaults), new HttpRouteValueDictionary(constraints))
        {
            this.Domain = domain;
        }

        public string Domain { get; set; }

        public override IHttpRouteData GetRouteData(string virtualPathRoot, System.Net.Http.HttpRequestMessage request)
        {
            // Build regex
            domainRegex = CreateRegex(this.Domain);
            pathRegex = CreateRegex(this.RouteTemplate);

            // Request information
            string requestDomain = request.Headers.Host;
            if (!string.IsNullOrEmpty(requestDomain))
            {
                if (requestDomain.IndexOf(":") > 0)
                {
                    requestDomain = requestDomain.Substring(0, requestDomain.IndexOf(":"));
                }
            }
            else
            {
                requestDomain = request.RequestUri.Host;
            }

            var httpContext = HttpContext.Current; // TODO: replace this with something that works for selfhost
            string requestPath = httpContext.Request.AppRelativeCurrentExecutionFilePath.Substring(2) + httpContext.Request.PathInfo;

            // Match domain and route
            Match domainMatch = domainRegex.Match(requestDomain);
            Match pathMatch = pathRegex.Match(requestPath);

            // Route data
            HttpRouteData data = null;
            if (domainMatch.Success && pathMatch.Success)
            {
                data = new HttpRouteData(this);

                // Add defaults first
                if (Defaults != null)
                {
                    foreach (KeyValuePair<string, object> item in Defaults)
                    {
                        data.Values[item.Key] = item.Value;
                    }
                }

                // Iterate matching domain groups
                for (int i = 1; i < domainMatch.Groups.Count; i++)
                {
                    Group group = domainMatch.Groups[i];
                    if (group.Success)
                    {
                        string key = domainRegex.GroupNameFromNumber(i);

                        if (!string.IsNullOrEmpty(key) && !char.IsNumber(key, 0))
                        {
                            if (!string.IsNullOrEmpty(group.Value))
                            {
                                data.Values[key] = group.Value;
                            }
                        }
                    }
                }

                // Iterate matching path groups
                for (int i = 1; i < pathMatch.Groups.Count; i++)
                {
                    Group group = pathMatch.Groups[i];
                    if (group.Success)
                    {
                        string key = pathRegex.GroupNameFromNumber(i);

                        if (!string.IsNullOrEmpty(key) && !char.IsNumber(key, 0))
                        {
                            if (!string.IsNullOrEmpty(group.Value))
                            {
                                data.Values[key] = group.Value;
                            }
                        }
                    }
                }
            }

            return data;
        }

        public override IHttpVirtualPathData GetVirtualPath(System.Net.Http.HttpRequestMessage request, IDictionary<string, object> values)
        {
            return base.GetVirtualPath(request, RemoveDomainTokens(values));
        }

        private Regex CreateRegex(string source)
        {
            // Perform replacements
            source = source.Replace("/", @"\/?");
            source = source.Replace(".", @"\.?");
            source = source.Replace("-", @"\-?");
            source = source.Replace("{", @"(?<");
            source = source.Replace("}", @">([a-zA-Z0-9_-]*))");

            return new Regex("^" + source + "$");
        }

        private IDictionary<string, object> RemoveDomainTokens(IDictionary<string, object> values)
        {
            Regex tokenRegex = new Regex(@"({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?({[a-zA-Z0-9_-]*})*-?\.?\/?");
            Match tokenMatch = tokenRegex.Match(Domain);
            for (int i = 0; i < tokenMatch.Groups.Count; i++)
            {
                Group group = tokenMatch.Groups[i];
                if (group.Success)
                {
                    string key = group.Value.Replace("{", "").Replace("}", "");
                    if (values.ContainsKey(key))
                        values.Remove(key);
                }
            }

            return values;
        }
    }
}
