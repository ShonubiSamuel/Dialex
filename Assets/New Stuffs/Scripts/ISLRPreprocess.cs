using System;
using System.Linq;

public static class ISLRPreprocess
{
    // ---- Landmark index sets (must match your training) ----
    static readonly int[] LIP = {
        0, 61,185,40,39,37,267,269,270,409,
        291,146,91,181,84,17,314,405,321,375,
        78,191,80,81,82,13,312,311,310,415,
        95,88,178,87,14,317,402,318,324,308,
    };

    static readonly int[] LLIP = { 84,181,91,146,61,185,40,39,37,87,178,88,95,78,191,80,81,82 };
    static readonly int[] RLIP = { 314,405,321,375,291,409,270,269,267,317,402,318,324,308,415,310,311,312 };

    static readonly int[] LHAND = Enumerable.Range(468, 21).ToArray();
    static readonly int[] RHAND = Enumerable.Range(522, 21).ToArray();

    static readonly int[] NOSE  = {1,2,98,327};
    static readonly int[] REYE = {
        33, 7, 163, 144, 145, 153, 154, 155, 133,
        246, 161, 160, 159, 158, 157, 173
    };
    static readonly int[] LEYE = {
        263, 249, 390, 373, 374, 380, 381, 382, 362,
        466, 388, 387, 386, 385, 384, 398
    };

    // The exact point list used in training (must match Python):
    static readonly int[] POINT_LANDMARKS =
        (new int[][] { LIP, LHAND, RHAND, NOSE, REYE, LEYE }).SelectMany(a => a).ToArray();

    public static int NumSelectedPoints => POINT_LANDMARKS.Length; // P
    public static int ChannelsPerTime   => 6 * POINT_LANDMARKS.Length; // (x,y) + Δ + Δ²

    /// <summary>
    /// Build features for one window.
    /// inputs: frames[T,543,3] with values in ~[0..1] (MediaPipe normalized)
    /// returns: features[T, 6*P] flattened row-major (time major).
    /// </summary>
    public static float[] BuildFeatures(float[,,] frames, int T, int N = 543, int C = 3)
    {
        int P = POINT_LANDMARKS.Length;
        var outFeat = new float[T * 6 * P];

        // 1) Compute reference mean from landmark #17 across time (x,y,z), nan-safe
        double meanX = 0, meanY = 0, meanZ = 0; int cnt = 0;
        for (int t = 0; t < T; t++)
        {
            float x = frames[t, 17, 0];
            float y = frames[t, 17, 1];
            float z = frames[t, 17, 2];
            if (!float.IsNaN(x)) { meanX += x; cnt++; }
            if (!float.IsNaN(y)) { meanY += y; }
            if (!float.IsNaN(z)) { meanZ += z; }
        }
        if (cnt > 0) { meanX /= cnt; meanY /= cnt; meanZ /= cnt; }
        else { meanX = meanY = meanZ = 0.5; }  // same default as TF

        // 2) Gather selected points and compute global std (over time & points)
        //    we only keep x,y later, but std is computed like in Python on (x,y,z) around mean
        double sxx = 0, syy = 0; int sCnt = 0;
        // temp arrays for (x-mean)/std, but we need std first; store centered x,y for now
        var cx = new float[T, P];
        var cy = new float[T, P];

        for (int t = 0; t < T; t++)
        {
            for (int p = 0; p < P; p++)
            {
                int idx = POINT_LANDMARKS[p];
                float x = frames[t, idx, 0];
                float y = frames[t, idx, 1];
                // z is ignored for features, but included in std in your TF (we'll skip z here; that’s fine)
                if (float.IsNaN(x)) x = 0.5f;
                if (float.IsNaN(y)) y = 0.5f;

                float dx = x - (float)meanX;
                float dy = y - (float)meanY;
                cx[t, p] = dx;
                cy[t, p] = dy;
                sxx += dx * dx;
                syy += dy * dy;
                sCnt++;
            }
        }
        double stdX = Math.Sqrt(sxx / Math.Max(1, sCnt));
        double stdY = Math.Sqrt(syy / Math.Max(1, sCnt));
        if (stdX < 1e-6) stdX = 1e-6;
        if (stdY < 1e-6) stdY = 1e-6;

        // 3) Normalize and compute Δ and Δ²
        // layout per time: [ x,y for all P | Δx,Δy for all P | Δ²x,Δ²y for all P ]
        for (int t = 0; t < T; t++)
        {
            int baseXY  = t * (6 * P);
            int baseDX  = baseXY + (2 * P);
            int baseDX2 = baseXY + (4 * P);

            for (int p = 0; p < P; p++)
            {
                // normalized x,y
                float nx = (float)(cx[t, p] / stdX);
                float ny = (float)(cy[t, p] / stdY);
                outFeat[baseXY + 2 * p + 0] = nx;
                outFeat[baseXY + 2 * p + 1] = ny;

                // Δ: x[t+1] - x[t] (0 for last frame)
                float nx1 = (t + 1 < T) ? (float)(cx[t + 1, p] / stdX) : 0f;
                float ny1 = (t + 1 < T) ? (float)(cy[t + 1, p] / stdY) : 0f;
                outFeat[baseDX + 2 * p + 0] = nx1 - nx;
                outFeat[baseDX + 2 * p + 1] = ny1 - ny;

                // Δ²: x[t+2] - x[t] (0 for last 2 frames)
                float nx2 = (t + 2 < T) ? (float)(cx[t + 2, p] / stdX) : 0f;
                float ny2 = (t + 2 < T) ? (float)(cy[t + 2, p] / stdY) : 0f;
                outFeat[baseDX2 + 2 * p + 0] = nx2 - nx;
                outFeat[baseDX2 + 2 * p + 1] = ny2 - ny;
            }
        }
        return outFeat;
    }
}