using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace YourApp.Signs.Pipeline.Net
{
    /// <summary>
    /// Lightweight HTTP wrapper for coroutines with retries, timeouts, and headers.
    /// Provides JSON POST/GET and multipart upload (for audio).
    /// </summary>
    public static class HttpClientUnity
    {
        // ---- Public convenience ----

        public static IEnumerator PostJson(
            string url,
            string jsonBody,
            int timeoutSeconds,
            RetryPolicy retry,
            IEnumerable<(string name, string value)> headers,
            Action<string> onDone,
            Action<Exception, long, string> onError = null)
        {
            UnityWebRequest Build()
            {
                var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                var body = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (headers != null) foreach (var h in headers) req.SetRequestHeader(h.name, h.value);
                req.timeout = Mathf.Max(1, timeoutSeconds);
                return req;
            }

            yield return SendWithRetry(Build, retry ?? RetryPolicy.Default, onDone, onError);
        }

        public static IEnumerator GetJson(
            string url,
            int timeoutSeconds,
            RetryPolicy retry,
            IEnumerable<(string name, string value)> headers,
            Action<string> onDone,
            Action<Exception, long, string> onError = null)
        {
            UnityWebRequest Build()
            {
                var req = UnityWebRequest.Get(url);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Accept", "application/json");
                if (headers != null) foreach (var h in headers) req.SetRequestHeader(h.name, h.value);
                req.timeout = Mathf.Max(1, timeoutSeconds);
                return req;
            }

            yield return SendWithRetry(Build, retry ?? RetryPolicy.Default, onDone, onError);
        }

        /// <summary>
        /// Multipart/form-data upload: file + fields (typical STT).
        /// </summary>
        public static IEnumerator PostMultipart(
            string url,
            byte[] fileBytes, string fileFieldName, string fileName, string mimeType,
            Dictionary<string, string> fields,
            int timeoutSeconds,
            RetryPolicy retry,
            IEnumerable<(string name, string value)> headers,
            Action<string> onDone,
            Action<Exception, long, string> onError = null)
        {
            UnityWebRequest Build()
            {
                var form = new WWWForm();
                if (fileBytes != null)
                    form.AddBinaryData(fileFieldName ?? "file", fileBytes, fileName ?? "file.bin", mimeType ?? "application/octet-stream");
                if (fields != null)
                    foreach (var kv in fields) form.AddField(kv.Key, kv.Value ?? "");

                var req = UnityWebRequest.Post(url, form);
                req.downloadHandler = new DownloadHandlerBuffer();
                if (headers != null) foreach (var h in headers) req.SetRequestHeader(h.name, h.value);
                req.timeout = Mathf.Max(1, timeoutSeconds);
                return req;
            }

            yield return SendWithRetry(Build, retry ?? RetryPolicy.Default, onDone, onError);
        }

        // ---- Core sender with retry policy ----

        private static IEnumerator SendWithRetry(
            Func<UnityWebRequest> buildRequest,
            RetryPolicy retry,
            Action<string> onDone,
            Action<Exception, long, string> onError)
        {
            Exception lastEx = null;
            long lastCode = 0;
            string lastBody = null;

            for (int attempt = 1; attempt <= Mathf.Max(1, retry.maxAttempts); attempt++)
            {
                using var req = buildRequest();

                yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool transportError = req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError;
                bool success = req.result == UnityWebRequest.Result.Success;
#else
                bool transportError = req.isNetworkError || req.isHttpError;
                bool success = !(req.isNetworkError || req.isHttpError);
#endif
                long code = req.responseCode;
                string body = req.downloadHandler != null ? req.downloadHandler.text : null;

                if (success)
                {
                    onDone?.Invoke(body);
                    yield break;
                }

                // Decide retry
                bool shouldRetry = retry.ShouldRetry((int)code, transportError);
                lastEx = new Exception($"HTTP {(code == 0 ? -1 : code)}: {req.error}");
                lastCode = code;
                lastBody = body;

                if (!shouldRetry || attempt == retry.maxAttempts)
                {
                    onError?.Invoke(lastEx, lastCode, lastBody);
                    yield break;
                }

                // Honor Retry-After (seconds) if present
                float delay = 0f;
                string retryAfter = req.GetResponseHeader("Retry-After");
                if (!string.IsNullOrEmpty(retryAfter) && float.TryParse(retryAfter, out var s) && s > 0f)
                {
                    delay = s;
                }

                if (delay > 0f) yield return new WaitForSeconds(delay);
                else yield return retry.WaitBackoff(attempt);
            }
        }

        // ---- Helpers to merge headers from ApiKeysConfig ----

        public static List<(string name, string value)> MakeHeaders(
            ApiKeysConfig keys,
            string providerId,                           // e.g., "openai", "groq"
            IEnumerable<(string name, string value)> extra = null)
        {
            var list = new List<(string, string)>();
            if (extra != null) list.AddRange(extra);

            if (keys != null && !string.IsNullOrEmpty(providerId))
            {
                var bearer = keys.GetBearerHeader(providerId);
                if (bearer.HasValue) list.Add(bearer.Value);
            }
            return list;
        }
    }
}
