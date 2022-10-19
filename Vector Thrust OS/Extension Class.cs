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
        public static bool IsAlive(this IMyTerminalBlock block)
        {
            return block.CubeGrid.GetCubeBlock(block.Position)?.FatBlock == block;
        }

        public static void GetList<T>(this MyIni config, ref List<T> inp, string section, string key, Func<bool> cond)
        {
            List<T> prev = new List<T>(inp);
            inp = new List<T>();

            try
            {
                string temp = config.Get(section, key).ToString();
                string[] temp1 = temp.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string t in temp1)
                {
                    inp.Add((T)Convert.ChangeType(t, typeof(T)));
                }

                if (!cond()) inp = prev;
            }
            catch
            {
                inp = prev;
            }
        }

        public static List<string> Between(this string STR, string STR1, string STR2 = "")
        {
            if (STR2.Equals("")) STR2 = STR1;
            return STR.Split(new string[] { STR1, STR2 }, StringSplitOptions.RemoveEmptyEntries).Where(it => STR.Contains(STR1 + it + STR2)).ToList();
        }

        /*public static Vector3D Project(this Vector3D a, Vector3D b)
        {// projects a onto b
            double aDotB = Vector3D.Dot(a, b);
            double bDotB = Vector3D.Dot(b, b);
            return b * aDotB / bDotB;
        }*/

        public static double Clamp(this double val, double min, double max) {
            return MathHelper.Clamp(val, min, max);
        }

        public static Vector3D NewLength(this Vector3D inp, double val = 1) {
            return inp.Normalized() * val;
        }

        public static StringBuilder AppendNR(this StringBuilder str, string value)
        {
            if (str.Length > 0 && value != null && value.Length > 0)
            {
                str.Replace(value, "");
            }
            str.Append(value);
            return str;
        }

        public static bool FilterThis(this IMyTerminalBlock b, IMyTerminalBlock b1) => b.CubeGrid == b1.CubeGrid;
        public static void Brake(this IMyMotorStator rotor) => rotor.TargetVelocityRPM = 0;
        public static void Brake(this IMyThrust thruster) => thruster.ThrustOverridePercentage = 0;

        /*public static Vector3D Reject(this Vector3D a, Vector3D b)
        {
            return VectorMath;//Vector3D.Reject(a, b);
        }*/

        public static Vector3D Normalized(this Vector3D vec)
        {
            if (Vector3D.IsZero(vec))
                return Vector3D.Zero;

            if (Vector3D.IsUnit(ref vec))
                return vec;

            return Vector3D.Normalize(vec);
        }


        public static Vector3D Clamp(this Vector3D v, double min, double max) => v.Normalized() * v.Length().Clamp(min, max);
        

        public static double Dot(this Vector3D a, Vector3D b)
        {
            return Vector3D.Dot(a, b);
        }

        // get movement and turn it into worldspace
        public static Vector3D GetWorldMoveIndicator(this IMyShipController cont)
        {
            return Vector3D.TransformNormal(cont.MoveIndicator, cont.WorldMatrix);
        }

        public static double NNaN(this double inp)
        {
            return double.IsNaN(inp) ? 1 * Math.Pow(10, -10) : inp;
        }

        public static float Pow(this float p, float n) {
            return (float)Math.Pow(p, n);
        }

        public static double Pow(this double p, double n) => Math.Pow(p, n);
        

        public static float NNaN(this float inp)
        {
            return float.IsNaN(inp) ? (float)(1 * Math.Pow(10, -10)) : (float)inp;
        }

        public static double R3(this double desired, double ifval, double isval)
        {
            return (desired * isval) / ifval;
        }

        public static int Count<T>(this List<T> el, List<T>[] args)
        {
            return el.Count + args.Sum(x => x.Count);
        }

        public static bool Empty<T>(this List<T> list) => list.Count == 0;

        public static bool Empty<T>(this T[] array) => array.Length == 0;

        public static bool Empty(this string st) => st.Length == 0;
            
        
        public static double Abs(this double d)
        {
            return Math.Abs(d);
        }

        public static string Sep<T>(this T i, string sep = "/")
        {
            return i + sep;
        }

        public static StringBuilder ProgressBar(this StringBuilder sb, double percent, int amount)
        {
            int a = (int)(percent * amount);
            int rz = amount - a;
            sb.Append("--=[").Append('|', a).Append('\'', rz).Append("]=--");
            return sb;
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

        public static String ToString(this Vector3D val)
        {
            return $"X:{val.X} Y:{val.Y} Z:{val.Z}";
        }

        public static String ToString(this Vector3D val, bool pretty)
        {
            if (!pretty)
                return ToString(val);
            else
                return $"X:{val.X}\nY:{val.Y}\nZ:{val.Z}\n";
        }

        public static String ToString(this bool val)
        {
            if (val)
            {
                return "true";
            }
            return "false";
        }
    }
}
