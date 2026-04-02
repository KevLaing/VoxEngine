using System;

namespace VoxEngine.Utils;

public class Perlin
{
    private int[] p = new int[512];
    public Perlin(DeterministicRandom rand)
    {
        for (int i = 0; i < 256; i++) p[i] = i;
        for (int i = 0; i < 256; i++) {
            int j = rand.Next(256);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 256; i++) p[i + 256] = p[i];
    }

    public double Noise(double x, double y, double z)
    {
        int X = (int)Math.Floor(x) & 255;
        int Y = (int)Math.Floor(y) & 255;
        int Z = (int)Math.Floor(z) & 255;

        x -= Math.Floor(x);
        y -= Math.Floor(y);
        z -= Math.Floor(z);

        double u = Fade(x);
        double v = Fade(y);
        double w = Fade(z);

        int A = p[X] + Y, AA = p[A] + Z, AB = p[A + 1] + Z;
        int B = p[X + 1] + Y, BA = p[B] + Z, BB = p[B + 1] + Z;

        return Lerp(w, Lerp(v, Lerp(u, Grad(p[AA], x, y, z), Grad(p[BA], x - 1, y, z)),
                               Lerp(u, Grad(p[AB], x, y - 1, z), Grad(p[BB], x - 1, y - 1, z))),
                       Lerp(v, Lerp(u, Grad(p[AA + 1], x, y, z - 1), Grad(p[BA + 1], x - 1, y, z - 1)),
                               Lerp(u, Grad(p[AB + 1], x, y - 1, z - 1), Grad(p[BB + 1], x - 1, y - 1, z - 1))));
    }
    double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    double Lerp(double t, double a, double b) => a + t * (b - a);
    double Grad(int hash, double x, double y, double z) => ((hash & 15) < 8 ? x : y) + ((hash & 15) < 4 ? y : ((hash & 15) == 12 || (hash & 15) == 14 ? x : z));
}
