using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IngameScript
{
    partial class Program
    {
        // RUNTIME TRACKER BY WHIPLASH, THANK YOU!!! :)
        class RuntimeTracker
        {
            public int Capacity { get; set; }
            public double Sensitivity { get; set; }
            public double MaxRuntime { get; private set; }
            public double MaxInstructions { get; private set; }
            public double AverageRuntime { get; private set; }
            public double AverageInstructions { get; private set; }
            public double LastRuntime { get; private set; }
            public double LastInstructions { get; private set; }

            readonly Queue<double> _runtimes = new Queue<double>();
            readonly Queue<double> _instructions = new Queue<double>();
            readonly StringBuilder _sb = new StringBuilder();
            readonly int _instructionLimit;
            readonly Program _program;
            const double MS_PER_TICK = 16.6666;
            double sumlastrun;
            public double tremaining = 0;
            public bool configtrigger = false;
            public StringBuilder actionsused = new StringBuilder();

            public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.005)
            {
                _program = program;
                Capacity = capacity;
                Sensitivity = sensitivity;
                _instructionLimit = _program.Runtime.MaxInstructionCount;
            }

            public void AddRuntime()
            {
                double tfr = _program.TimeForRefresh;
                bool config = sumlastrun < tfr;

                if (_program.pc >= 1000) { 
                    _program.pc = 0;
                    _program.log.Clear();
                }
                if (_program.Runtime.UpdateFrequency == UpdateFrequency.Update1) _program.pc++;
                else if (_program.Runtime.UpdateFrequency == UpdateFrequency.Update10) _program.pc += 10;
                else _program.pc += 100;

                if (!configtrigger && !config)
                {
                    sumlastrun = 0;
                }

                if (config)
                {
                    double tslrs = _program.Runtime.TimeSinceLastRun.TotalSeconds;
                    sumlastrun += tslrs;
                    tremaining = (tfr - sumlastrun).Round(1);
                }
                else
                {
                    configtrigger = true;
                }


                double runtime = _program.Runtime.LastRunTimeMs;

                LastRuntime = runtime;
                AverageRuntime += (Sensitivity * runtime);
                int roundedTicksSinceLastRuntime = (int)Math.Round(_program.Runtime.TimeSinceLastRun.TotalMilliseconds / MS_PER_TICK);
                if (roundedTicksSinceLastRuntime == 1)
                {
                    AverageRuntime *= (1 - Sensitivity);
                }
                else if (roundedTicksSinceLastRuntime > 1)
                {
                    AverageRuntime *= Math.Pow((1 - Sensitivity), roundedTicksSinceLastRuntime);
                }

                _runtimes.Enqueue(runtime);
                if (_runtimes.Count == Capacity)
                {
                    _runtimes.Dequeue();
                }

                if (actionsused.Length > 0 && LastRuntime > 0.09) {
                    _program.log.AppendNR($"!{LastRuntime}: {actionsused}\n");
                }
                actionsused.Clear();

                MaxRuntime = _runtimes.Max();
            }

            public void AddInstructions()
            {
                double instructions = _program.Runtime.CurrentInstructionCount;
                LastInstructions = instructions;
                AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;

                _instructions.Enqueue(instructions);
                if (_instructions.Count == Capacity)
                {
                    _instructions.Dequeue();
                }

                MaxInstructions = _instructions.Max();
            }

            public string Write()
            {
                _sb.Clear();
                _sb.AppendLine($"---Performance---");
                _sb.AppendLine($" -Instructions-");
                _sb.AppendLine($"   Avg: {AverageInstructions:n2}");
                _sb.AppendLine($"   Last: {LastInstructions:n0}");
                _sb.AppendLine($"   Max: {MaxInstructions:n0}");
                _sb.AppendLine($"   Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
                _sb.AppendLine($" -Runtime-");
                _sb.AppendLine($"   Avg: {AverageRuntime:n4} ms");
                _sb.AppendLine($"   Last: {LastRuntime:n4} ms");
                _sb.AppendLine($"   Max [{Capacity}]: {MaxRuntime:n4} ms");
                return _sb.ToString();
            }

            public StringBuilder Append(StringBuilder sba)
            {
                sba.AppendLine($"--- Performance ---");
                sba.AppendLine($" - Instructions -");
                sba.AppendLine($"   >Avg: {AverageInstructions:n2}");
                sba.AppendLine($"   >Last: {LastInstructions:n0}");
                sba.AppendLine($"   >Max: {MaxInstructions:n0}");
                sba.AppendLine($"   >Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
                sba.AppendLine($" - Runtime -");
                sba.AppendLine($"   >Avg: {AverageRuntime:n4} ms");
                sba.AppendLine($"   >Last: {LastRuntime:n4} ms");
                sba.AppendLine($"   >Max [{Capacity}]: {MaxRuntime:n4} ms");
                return sba;
            }

            public void RegisterAction(string s) {
                actionsused.Append(s + "/");
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
