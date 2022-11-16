using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {

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

        class Tracker
        {

            public double MaxRuntime { get; private set; }
            public double AverageRuntime { get; private set; }
            public double LastRuntime { get; private set; }
            public bool CanPrint { get; private set; }

            public EMA ema;

            int FrameCount = 0;
            int FramePrintCount = 0;
            readonly int MaxCapacity;

            readonly Program p;

            public Tracker(Program p, int avgcap, int maxcap)
            {
                this.p = p;

                MaxCapacity = maxcap;
                ema = new EMA(avgcap);
            }

            public void ChangeAvgCapacity(int i)
            {
                ema = new EMA(i);
            }

            public void Process()
            {
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
