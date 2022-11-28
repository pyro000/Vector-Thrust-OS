using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    static class Extensions
    {
        public static bool IsConnected(this IMyThrust thruster)
        {
            return thruster.IsWorking || (!thruster.IsWorking && (!thruster.Enabled || !thruster.IsFunctional));
        }

        public static double Truncate(this double n, int d) {
            double pow = Math.Pow(10, d);
            return Math.Truncate(n * pow) / pow;
        }

        public static float Truncate(this float n, int d)
        {
            double pow = Math.Pow(10, d);
            return (float)(Math.Truncate(n * pow) / pow);
        }

        public static List<T> GetList<T>(this MyIni config, string section, string key)
        {
            List<T> result = new List<T>();
            try
            {
                string temp = config.Get(section, key).ToString();
                string[] temp1 = temp.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string t in temp1)
                {
                    result.Add((T)Convert.ChangeType(t, typeof(T)));
                }
            }
            catch {}
            return result;
        }

        public static List<string> Between(this string STR, string STR1, string STR2 = "")
        {
            if (STR2.Equals("")) STR2 = STR1;
            return STR.Split(new string[] { STR1, STR2 }, StringSplitOptions.RemoveEmptyEntries).Where(it => STR.Contains(STR1 + it + STR2)).ToList();
        }

        public static double Clamp(this double val, double min, double max)
        {
            return MathHelper.Clamp(val, min, max);
        }

        public static Vector3D NewLength(this Vector3D inp, double val = 1)
        {
            return inp.Normalized() * val;
        }

        public static StringBuilder AppendNR(this StringBuilder str, string value)
        {
            if (str/*.Length > 0*/.Empty() && value != null && value/*.Length > 0*/.Empty())
            {
                str.Replace(value, "");
            }
            str.Append(value);
            return str;
        }

        public static bool FilterThis(this IMyTerminalBlock b, IMyTerminalBlock b1) => b.CubeGrid == b1.CubeGrid;
        public static void Brake(this IMyMotorStator rotor) => rotor.TargetVelocityRPM = 0;
        public static void Brake(this IMyThrust thruster) => thruster.ThrustOverridePercentage = 0;

        public static Vector3D Normalized(this Vector3D vec)
        {
            if (Vector3D.IsZero(vec))
                return Vector3D.Zero;

            if (Vector3D.IsUnit(ref vec))
                return vec;

            return Vector3D.Normalize(vec);
        }

        public static Vector3D Clamp(this Vector3D v, double min, double max) => v.Normalized() * v.Length().Clamp(min, max);

        public static int Clamp(this int v, int min, int max) => MathHelper.Clamp(v, min, max);
        public static double Dot(this Vector3D a, Vector3D b)
        {
            return Vector3D.Dot(a, b);
        }

        // get movement and turn it into worldspace
        public static Vector3D GetWorldMoveIndicator(this IMyShipController cont)
        {
            return Vector3D.TransformNormal(cont.MoveIndicator, cont.WorldMatrix);
        }

        public static float Pow(this float p, float n)
        {
            return (float)Math.Pow(p, n);
        }

        public static double Pow(this double p, double n) => Math.Pow(p, n);

        public static bool Empty<T>(this List<T> list) => list.Count == 0;

        //public static bool Empty<T>(this T[] array) => array.Length == 0;

        public static bool Empty(this string st) => st.Length == 0;
        public static bool Empty(this StringBuilder st) => st.Length == 0;

        public static double Abs(this double d)
        {
            return Math.Abs(d);
        }

        public static float Abs(this float d)
        {
            return Math.Abs(d);
        }

        public static Vector3D Round(this Vector3D vec, int num = 0)
        {
            return Vector3D.Round(vec, num);
        }

        public static double Round(this double val, int num = 0)
        {
            return Math.Round(val, num);
        }

        public static float Round(this float val, int num = 0)
        {
            return (float)Math.Round(val, num);
        }
    }
}
