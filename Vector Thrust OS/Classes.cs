using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Interfaces;
using IMyProgrammableBlock = Sandbox.ModAPI.Ingame.IMyProgrammableBlock;
using UpdateFrequency = Sandbox.ModAPI.Ingame.UpdateFrequency;
using VRageMath;
using MyGridProgram = Sandbox.ModAPI.Ingame.MyGridProgram;

namespace IngameScript
{
    partial class Program
    {
        public class DebugAPI
        {
            public readonly bool ModDetected;

            public void RemoveDraw() => _removeDraw?.Invoke(_pb);
            Action<IMyProgrammableBlock> _removeDraw;

            public void RemoveAll() => _removeAll?.Invoke(_pb);
            Action<IMyProgrammableBlock> _removeAll;

            public void Remove(int id) => _remove?.Invoke(_pb, id);
            Action<IMyProgrammableBlock, int> _remove;

            public int DrawPoint(Vector3D origin, Color color, float radius = 0.2f, float seconds = DefaultSeconds, bool? onTop = null) => _point?.Invoke(_pb, origin, color, radius, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, Vector3D, Color, float, float, bool, int> _point;

            public int DrawLine(Vector3D start, Vector3D end, Color color, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _line?.Invoke(_pb, start, end, color, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, Vector3D, Vector3D, Color, float, float, bool, int> _line;

            public int DrawAABB(BoundingBoxD bb, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _aabb?.Invoke(_pb, bb, color, (int)style, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, BoundingBoxD, Color, int, float, float, bool, int> _aabb;

            public int DrawOBB(MyOrientedBoundingBoxD obb, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _obb?.Invoke(_pb, obb, color, (int)style, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, MyOrientedBoundingBoxD, Color, int, float, float, bool, int> _obb;

            public int DrawSphere(BoundingSphereD sphere, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, int lineEveryDegrees = 15, float seconds = DefaultSeconds, bool? onTop = null) => _sphere?.Invoke(_pb, sphere, color, (int)style, thickness, lineEveryDegrees, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, BoundingSphereD, Color, int, float, int, float, bool, int> _sphere;

            public int DrawMatrix(MatrixD matrix, float length = 1f, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _matrix?.Invoke(_pb, matrix, length, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, MatrixD, float, float, float, bool, int> _matrix;

            public int DrawGPS(string name, Vector3D origin, Color? color = null, float seconds = DefaultSeconds) => _gps?.Invoke(_pb, name, origin, color, seconds) ?? -1;
            Func<IMyProgrammableBlock, string, Vector3D, Color?, float, int> _gps;

            public int PrintHUD(string message, Font font = Font.Debug, float seconds = 2) => _printHUD?.Invoke(_pb, message, font.ToString(), seconds) ?? -1;
            Func<IMyProgrammableBlock, string, string, float, int> _printHUD;

            public void PrintChat(string message, string sender = null, Color? senderColor = null, Font font = Font.Debug) => _chat?.Invoke(_pb, message, sender, senderColor, font.ToString());
            Action<IMyProgrammableBlock, string, string, Color?, string> _chat;

            public void DeclareAdjustNumber(out int id, double initial, double step = 0.05, Input modifier = Input.Control, string label = null) => id = _adjustNumber?.Invoke(_pb, initial, step, modifier.ToString(), label) ?? -1;
            Func<IMyProgrammableBlock, double, double, string, string, int> _adjustNumber;

            public double GetAdjustNumber(int id, double noModDefault = 1) => _getAdjustNumber?.Invoke(_pb, id) ?? noModDefault;
            Func<IMyProgrammableBlock, int, double> _getAdjustNumber;

            public int GetTick() => _tick?.Invoke() ?? -1;
            Func<int> _tick;

            public enum Style { Solid, Wireframe, SolidAndWireframe }
            public enum Input { MouseLeftButton, MouseRightButton, MouseMiddleButton, MouseExtraButton1, MouseExtraButton2, LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt, Tab, Shift, Control, Alt, Space, PageUp, PageDown, End, Home, Insert, Delete, Left, Up, Right, Down, D0, D1, D2, D3, D4, D5, D6, D7, D8, D9, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9, Multiply, Add, Separator, Subtract, Decimal, Divide, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
            public enum Font { Debug, White, Red, Green, Blue, DarkBlue }

            const float DefaultThickness = 0.02f;
            const float DefaultSeconds = -1;

            IMyProgrammableBlock _pb;
            bool _defaultOnTop;

            public DebugAPI(MyGridProgram program, bool drawOnTopDefault = false)
            {
                if (program == null)
                    throw new Exception("Pass `this` into the API, not null.");

                _defaultOnTop = drawOnTopDefault;
                _pb = program.Me;

                var methods = _pb.GetProperty("DebugAPI")?.As<IReadOnlyDictionary<string, Delegate>>()?.GetValue(_pb);
                if (methods != null)
                {
                    Assign(out _removeAll, methods["RemoveAll"]);
                    Assign(out _removeDraw, methods["RemoveDraw"]);
                    Assign(out _remove, methods["Remove"]);
                    Assign(out _point, methods["Point"]);
                    Assign(out _line, methods["Line"]);
                    Assign(out _aabb, methods["AABB"]);
                    Assign(out _obb, methods["OBB"]);
                    Assign(out _sphere, methods["Sphere"]);
                    Assign(out _matrix, methods["Matrix"]);
                    Assign(out _gps, methods["GPS"]);
                    Assign(out _printHUD, methods["HUDNotification"]);
                    Assign(out _chat, methods["Chat"]);
                    Assign(out _adjustNumber, methods["DeclareAdjustNumber"]);
                    Assign(out _getAdjustNumber, methods["GetAdjustNumber"]);
                    Assign(out _tick, methods["Tick"]);
                    RemoveAll();
                    ModDetected = true;
                }
            }

            void Assign<T>(out T field, object method) => field = (T)method;
        }

        public static class VectorMath // By Whiplash
        {
            /// <summary>
            ///  Normalizes a vector only if it is non-zero and non-unit
            /// </summary>
            public static Vector3D SafeNormalize(Vector3D a)
            {
                if (Vector3D.IsZero(a))
                    return Vector3D.Zero;

                if (Vector3D.IsUnit(ref a))
                    return a;

                return Vector3D.Normalize(a);
            }

            /// <summary>
            /// Reflects vector a over vector b with an optional rejection factor
            /// </summary>
            public static Vector3D Reflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b
            {
                Vector3D project_a = Projection(a, b);
                Vector3D reject_a = a - project_a;
                return project_a - reject_a * rejectionFactor;
            }

            /// <summary>
            /// Rejects vector a on vector b
            /// </summary>
            public static Vector3D Rejection(Vector3D a, Vector3D b) //reject a on b
            {
                if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                    return Vector3D.Zero;

                return a - a.Dot(b) / b.LengthSquared() * b;
            }

            /// <summary>
            /// Projects vector a onto vector b
            /// </summary>
            public static Vector3D Projection(Vector3D a, Vector3D b)
            {
                if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                    return Vector3D.Zero;

                if (Vector3D.IsUnit(ref b))
                    return a.Dot(b) * b;

                return a.Dot(b) / b.LengthSquared() * b;
            }

            /// <summary>
            /// Scalar projection of a onto b
            /// </summary>
            public static double ScalarProjection(Vector3D a, Vector3D b)
            {
                if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                    return 0;

                if (Vector3D.IsUnit(ref b))
                    return a.Dot(b);

                return a.Dot(b) / b.Length();
            }

            /// <summary>
            /// Computes angle between 2 vectors in radians.
            /// </summary>
            public static double AngleBetween(Vector3D a, Vector3D b)
            {
                if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                    return 0;
                else
                    return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
            }

            /// <summary>
            /// Computes cosine of the angle between 2 vectors.
            /// </summary>
            public static double CosBetween(Vector3D a, Vector3D b)
            {
                if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
                    return 0;
                else
                    return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
            }

            /// <summary>
            /// Returns if the normalized dot product between two vectors is greater than the tolerance.
            /// This is helpful for determining if two vectors are "more parallel" than the tolerance.
            /// </summary>
            /// <param name="a">First vector</param>
            /// <param name="b">Second vector</param>
            /// <param name="tolerance">Cosine of maximum angle</param>
            /// <returns></returns>
            public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
            {
                double dot = Vector3D.Dot(a, b);
                double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
                return Math.Abs(dot) * dot > num;
            }
        }

        // RUNTIME TRACKER BY WHIPLASH, THANK YOU!!! :)

        public class EMA
        {
            private bool _isInitialized;
            private readonly double _weightingMultiplier;
            private double _previousAverage;

            public double Average { get; private set; }
            public double Slope { get; private set; }

            public EMA(int lookback)
            {
                _weightingMultiplier = 2.0 / (lookback + 1);
            }

            public void AddDataPoint(double dataPoint)
            {
                if (!_isInitialized)
                {
                    Average = dataPoint;
                    Slope = 0;
                    _previousAverage = Average;
                    _isInitialized = true;
                    return;
                }

                Average = ((dataPoint - _previousAverage) * _weightingMultiplier) + _previousAverage;
                Slope = Average - _previousAverage;

                //update previous average
                _previousAverage = Average;
            }
        }

        class Tracker {
            
            public double MaxRuntime { get; private set; }
            public double AverageRuntime { get; private set; }
            public double LastRuntime { get; private set; }
            public bool CanPrint { get; private set; }

            public EMA ema;

            int FrameCount = 0;
            int FramePrintCount = 0;
            readonly int MaxCapacity;

            readonly Program p;

            public Tracker(Program p, int avgcap, int maxcap) {
                this.p = p;
               
                MaxCapacity = maxcap;
                ema = new EMA(avgcap);
            }

            public void ChangeAvgCapacity(int i) {
                ema = new EMA(i);
            }

            public void Process() {
                LastRuntime = p.Runtime.LastRunTimeMs;

                ema.AddDataPoint(LastRuntime);
                AverageRuntime = ema.Average;

                if (FrameCount % MaxCapacity == 0) MaxRuntime = AverageRuntime;
                else if (p.Runtime.LastRunTimeMs > MaxRuntime) MaxRuntime = LastRuntime;

                if (FrameCount >= MaxCapacity)
                {
                    FrameCount = 0;
                    p.log.Clear();
                }
                if (p.Runtime.UpdateFrequency == UpdateFrequency.Update1)
                {
                    FrameCount++;
                    FramePrintCount++;
                }
                else if (p.Runtime.UpdateFrequency == UpdateFrequency.Update10)
                {
                    FrameCount += 10;
                    FramePrintCount += 10;
                }
                else
                {
                    FrameCount += 100;
                    FramePrintCount += 100;
                }

                CanPrint = FramePrintCount >= p.framesperprint;
                if (CanPrint) FramePrintCount = 0;
            }
        }

        // Thanks to Digi for creating this example class
        class SimpleTimerSM
        {
            public readonly Program Program;
            public bool AutoStart { get; set; }
            public bool Running { get; private set; }
            public IEnumerable<double> Sequence;
            public double SequenceTimer { get; private set; }

            private IEnumerator<double> sequenceSM;
            public bool Doneloop { get; set; }

            public SimpleTimerSM(Program program, IEnumerable<double> sequence = null, bool autoStart = false)
            {
                Program = program;
                Sequence = sequence;
                AutoStart = autoStart;

                if (AutoStart)
                {
                    Start();
                }
            }
            public void Start()
            {
                Doneloop = false;
                SetSequenceSM(Sequence);
            }
            public void Run()
            {
                if (sequenceSM == null)
                    return;

                SequenceTimer -= Program.Runtime.TimeSinceLastRun.TotalSeconds;

                if (SequenceTimer > 0)
                    return;

                bool hasValue = sequenceSM.MoveNext();

                if (hasValue)
                {
                    SequenceTimer = sequenceSM.Current;

                    if (SequenceTimer <= -0.5)
                        hasValue = false;
                }

                if (!hasValue)
                {
                    if (AutoStart)
                        SetSequenceSM(Sequence);
                    else
                        SetSequenceSM(null);
                }
            }

            private void SetSequenceSM(IEnumerable<double> seq)
            {
                Running = false;
                SequenceTimer = 0;

                sequenceSM?.Dispose();
                sequenceSM = null;

                if (seq != null)
                {
                    Running = true;
                    sequenceSM = seq.GetEnumerator();
                }
            }
        }

        #region PID Class

        /// <summary>
        /// Discrete time PID controller class.
        /// Last edited: 2022/08/11 - Whiplash141
        /// </summary>
        public class PID
        {
            public double Kp { get; set; } = 0;
            public double Ki { get; set; } = 0;
            public double Kd { get; set; } = 0;
            public double Value { get; private set; }

            double _timeStep = 0;
            double _inverseTimeStep = 0;
            double _errorSum = 0;
            double _lastError = 0;
            bool _firstRun = true;

            public PID(double kp, double ki, double kd, double timeStep)
            {
                Kp = kp;
                Ki = ki;
                Kd = kd;
                _timeStep = timeStep;
                _inverseTimeStep = 1 / _timeStep;
            }

            protected virtual double GetIntegral(double currentError, double errorSum, double timeStep)
            {
                return errorSum + currentError * timeStep;
            }

            public double Control(double error)
            {
                //Compute derivative term
                double errorDerivative = (error - _lastError) * _inverseTimeStep;

                if (_firstRun)
                {
                    errorDerivative = 0;
                    _firstRun = false;
                }

                //Get error sum
                _errorSum = GetIntegral(error, _errorSum, _timeStep);

                //Store this error as last error
                _lastError = error;

                //Construct output
                Value = Kp * error + Ki * _errorSum + Kd * errorDerivative;
                return Value;
            }

            public double Control(double error, double timeStep)
            {
                if (timeStep != _timeStep)
                {
                    _timeStep = timeStep;
                    _inverseTimeStep = 1 / _timeStep;
                }
                return Control(error);
            }

            public virtual void Reset()
            {
                _errorSum = 0;
                _lastError = 0;
                _firstRun = true;
            }
        }

        public class DecayingIntegralPID : PID
        {
            public double IntegralDecayRatio { get; set; }

            public DecayingIntegralPID(double kp, double ki, double kd, double timeStep, double decayRatio) : base(kp, ki, kd, timeStep)
            {
                IntegralDecayRatio = decayRatio;
            }

            protected override double GetIntegral(double currentError, double errorSum, double timeStep)
            {
                return errorSum * (1.0 - IntegralDecayRatio) + currentError * timeStep;
            }
        }

        public class ClampedIntegralPID : PID
        {
            public double IntegralUpperBound { get; set; }
            public double IntegralLowerBound { get; set; }

            public ClampedIntegralPID(double kp, double ki, double kd, double timeStep, double lowerBound, double upperBound) : base(kp, ki, kd, timeStep)
            {
                IntegralUpperBound = upperBound;
                IntegralLowerBound = lowerBound;
            }

            protected override double GetIntegral(double currentError, double errorSum, double timeStep)
            {
                errorSum += currentError * timeStep;
                return Math.Min(IntegralUpperBound, Math.Max(errorSum, IntegralLowerBound));
            }
        }

        public class BufferedIntegralPID : PID
        {
            readonly Queue<double> _integralBuffer = new Queue<double>();
            public int IntegralBufferSize { get; set; } = 0;

            public BufferedIntegralPID(double kp, double ki, double kd, double timeStep, int bufferSize) : base(kp, ki, kd, timeStep)
            {
                IntegralBufferSize = bufferSize;
            }

            protected override double GetIntegral(double currentError, double errorSum, double timeStep)
            {
                if (_integralBuffer.Count == IntegralBufferSize)
                    _integralBuffer.Dequeue();
                _integralBuffer.Enqueue(currentError * timeStep);
                return _integralBuffer.Sum();
            }

            public override void Reset()
            {
                base.Reset();
                _integralBuffer.Clear();
            }
        }
        #endregion

    }
}
