using Fitbit.Api;
using Fitbit.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Fitbit.Helpers
{
    static class RestHelper
    {
        public static TResult ExecutePostRequest<TResult>(this IRestClient restClient, string Uri, string userId, string rootElement, ArgumentList args)
            where TResult : new()
        {
            string userSignifier = "-"; // used for current user
            if (!string.IsNullOrWhiteSpace(userId))
            {
                userSignifier = userId;
            }

            string endPoint = string.Format(Uri, userSignifier);
            RestRequest request = new RestRequest(endPoint, Method.POST);
            request.RootElement = rootElement;

            foreach (var arg in args)
            {
                AddPostParameter(request, arg.Key, arg.Value);
            }

            var response = restClient.Execute<TResult>(request);

            HandleResponse(response);

            return response.Data;
        }

        private static void AddPostParameter(IRestRequest request, string name, object value)
        {
            Parameter p = new Parameter();
            p.Type = ParameterType.GetOrPost;
            p.Name = name;
            p.Value = value;
            request.AddParameter(p);
        }

        /// <summary>
        /// Generic handling of status responses
        /// See: https://wiki.fitbit.com/display/API/API+Response+Format+And+Errors
        /// </summary>
        /// <param name="httpStatusCode"></param>
        public static void HandleResponse(IRestResponse response)
        {
            System.Net.HttpStatusCode httpStatusCode = response.StatusCode;
            if (httpStatusCode == System.Net.HttpStatusCode.OK ||        //200
                httpStatusCode == System.Net.HttpStatusCode.Created ||   //201
                httpStatusCode == System.Net.HttpStatusCode.NoContent)   //204
            {
                return;
            }
            else
            {
                Console.WriteLine("HttpError:" + httpStatusCode.ToString());
                IList<ApiError> errors;
                try
                {
                    var xmlDeserializer = new RestSharp.Deserializers.XmlDeserializer() { RootElement = "errors" };
                    errors = xmlDeserializer.Deserialize<List<ApiError>>(new RestResponse { Content = response.Content });
                }
                catch (Exception) // If there's an issue deserializing the error we still want to raise a fitbit exception
                {
                    errors = new List<ApiError>();
                }

                FitbitException exception = new FitbitException("Http Error:" + httpStatusCode.ToString(), httpStatusCode, errors);

                var retryAfterHeader = response.Headers.FirstOrDefault(h => h.Name == "Retry-After");
                if (retryAfterHeader != null)
                {
                    int retryAfter;
                    if (int.TryParse(retryAfterHeader.Value.ToString(), out retryAfter))
                    {
                        exception.retryAfter = retryAfter;
                    }
                }
                throw exception;
            }
        }
    }
}
