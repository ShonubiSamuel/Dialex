using System.Collections;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Net
{
    /// <summary>
    /// Simple retry policy: exponential backoff with jitter.
    /// Intended to be used by HttpClientUnity around UnityWebRequest.
    /// </summary>
    [System.Serializable]
    public class RetryPolicy
    {
        [Tooltip("Max attempts including the first try.")]
        public int maxAttempts = 3;
        [Tooltip("Initial backoff in seconds.")]
        public float baseDelay = 0.5f;
        [Tooltip("Max backoff in seconds.")]
        public float maxDelay = 8f;
        [Tooltip("Add small random jitter (0..jitter).")]
        public float jitter = 0.25f;

        [Tooltip("Retry on HTTP 429 (rate limit).")]
        public bool retryOn429 = true;
        [Tooltip("Retry on HTTP 5xx.")]
        public bool retryOn5xx = true;
        [Tooltip("Retry on transport errors (no response).")]
        public bool retryOnTransportError = true;

        /// <summary>Return true if this HTTP status should be retried.</summary>
        public bool ShouldRetry(int statusCode, bool transportError)
        {
            if (transportError) return retryOnTransportError;
            if (statusCode == 429) return retryOn429;
            if (statusCode >= 500 && statusCode <= 599) return retryOn5xx;
            return false;
        }

        /// <summary>Yield the backoff for attempt index (1-based).</summary>
        public IEnumerator WaitBackoff(int attemptIndex)
        {
            float delay = Mathf.Min(maxDelay, baseDelay * Mathf.Pow(2f, attemptIndex - 1));
            if (jitter > 0f) delay += Random.Range(0f, jitter);
            if (delay > 0f) yield return new WaitForSeconds(delay);
        }

        public static RetryPolicy Default => new RetryPolicy();
    }
}