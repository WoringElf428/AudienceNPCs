using UnityEngine;

public static class TarutaruUtil
{
    // 次に spareValue があれば、それを返すロジック用
    private static bool hasSpare = false;
    private static float spareValue;

    /// <summary>
    /// Box–Muller 法により、平均 mean、標準偏差 stdDev のガウス分布に従う乱数を返す。
    /// stdDev を省略した場合は 1.0f が使われる。
    /// </summary>
    public static float GaussianRandom(float mean, float stdDev = 1f)
    {
        if (hasSpare)
        {
            hasSpare = false;
            return spareValue * stdDev + mean;
        }
        else
        {
            float u, v, s;
            do
            {
                // Random.Range(-1f, 1f) でも可
                u = Random.value * 2f - 1f;
                v = Random.value * 2f - 1f;
                s = u * u + v * v;
            } while (s >= 1f || s == 0f);

            float mul = Mathf.Sqrt(-2f * Mathf.Log(s) / s);
            spareValue = v * mul;
            hasSpare = true;
            return mean + stdDev * (u * mul);
        }
    }
    public static int RandomIndexfromVec4(Vector4 probabilities)
    {
        float randomValue = UnityEngine.Random.value; // 0.0から1.0の間の乱数を生成

        if (randomValue < probabilities.x)
        {
            return 0;
        }
        else if (randomValue < probabilities.x + probabilities.y)
        {
            return 1;
        }
        else if (randomValue < probabilities.x + probabilities.y + probabilities.z)
        {
            return 2;
        }
        else
        {
            return 3;
        }
    }
}
