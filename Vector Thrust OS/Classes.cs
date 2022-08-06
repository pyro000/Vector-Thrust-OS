using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

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

				_program.globalAppend = false;

				if (_program.pc >= 1000) _program.pc = 0;
				if (_program.Runtime.UpdateFrequency == _program.update_frequency) _program.pc++;
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
		}

		public class PidController
		{
			private double processVariable = 0;

			public PidController(double GainProportional, double GainIntegral, double GainDerivative, double OutputMax, double OutputMin)
			{
				this.GainDerivative = GainDerivative;
				this.GainIntegral = GainIntegral;
				this.GainProportional = GainProportional;
				this.OutputMax = OutputMax;
				this.OutputMin = OutputMin;
			}

			/// <summary>
			/// The controller output
			/// </summary>
			/// <param name="timeSinceLastUpdate">timespan of the elapsed time
			/// since the previous time that ControlVariable was called</param>
			/// <returns>Value of the variable that needs to be controlled</returns>
			public double ControlVariable(TimeSpan timeSinceLastUpdate)
			{
				double error = SetPoint - ProcessVariable;

				// integral term calculation
				IntegralTerm += (GainIntegral * error * timeSinceLastUpdate.TotalSeconds);
				IntegralTerm = Clamp(IntegralTerm);

				// derivative term calculation
				double dInput = processVariable - ProcessVariableLast;
				double derivativeTerm = GainDerivative * (dInput / timeSinceLastUpdate.TotalSeconds);

				// proportional term calcullation
				double proportionalTerm = GainProportional * error;

				double output = proportionalTerm + IntegralTerm - derivativeTerm;

				output = Clamp(output);

				return output;
			}

			/// <summary>
			/// The derivative term is proportional to the rate of
			/// change of the error
			/// </summary>
			public double GainDerivative { get; set; } = 0;

			/// <summary>
			/// The integral term is proportional to both the magnitude
			/// of the error and the duration of the error
			/// </summary>
			public double GainIntegral { get; set; } = 0;

			/// <summary>
			/// The proportional term produces an output value that
			/// is proportional to the current error value
			/// </summary>
			/// <remarks>
			/// Tuning theory and industrial practice indicate that the
			/// proportional term should contribute the bulk of the output change.
			/// </remarks>
			public double GainProportional { get; set; } = 0;

			/// <summary>
			/// The max output value the control device can accept.
			/// </summary>
			public double OutputMax { get; private set; } = 0;

			/// <summary>
			/// The minimum ouput value the control device can accept.
			/// </summary>
			public double OutputMin { get; private set; } = 0;

			/// <summary>
			/// Adjustment made by considering the accumulated error over time
			/// </summary>
			/// <remarks>
			/// An alternative formulation of the integral action, is the
			/// proportional-summation-difference used in discrete-time systems
			/// </remarks>
			public double IntegralTerm { get; private set; } = 0;


			/// <summary>
			/// The current value
			/// </summary>
			public double ProcessVariable
			{
				get { return processVariable; }
				set
				{
					ProcessVariableLast = processVariable;
					processVariable = value;
				}
			}

			/// <summary>
			/// The last reported value (used to calculate the rate of change)
			/// </summary>
			public double ProcessVariableLast { get; private set; } = 0;

			/// <summary>
			/// The desired value
			/// </summary>
			public double SetPoint { get; set; } = 0;

			/// <summary>
			/// Limit a variable to the set OutputMax and OutputMin properties
			/// </summary>
			/// <returns>
			/// A value that is between the OutputMax and OutputMin properties
			/// </returns>
			/// <remarks>
			/// Inspiration from http://stackoverflow.com/questions/3176602/how-to-force-a-number-to-be-in-a-range-in-c
			/// </remarks>
			private double Clamp(double variableToClamp)
			{
				if (variableToClamp <= OutputMin) { return OutputMin; }
				if (variableToClamp >= OutputMax) { return OutputMax; }
				return variableToClamp;
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
		// THANK YOU WHIP!!! 
		/// <summary>
		/// Discrete time PID controller class.
		/// (Whiplash141 - 11/22/2018)
		/// </summary>
		public class PID
		{
			readonly double _kP = 0;
			readonly double _kI = 0;
			readonly double _kD = 0;

			double _timeStep = 0;
			double _inverseTimeStep = 0;
			double _errorSum = 0;
			public double _lastError = 0;
			bool _firstRun = true;

			public double Value { get; private set; }

			public PID(double kP, double kI, double kD, double timeStep)
			{
				_kP = kP;
				_kI = kI;
				_kD = kD;
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
				var errorDerivative = (error - _lastError) * _inverseTimeStep;

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
				this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
				return this.Value;
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

			public void Reset()
			{
				_errorSum = 0;
				_lastError = 0;
				_firstRun = true;
			}
		}

		public class DecayingIntegralPID : PID
		{
			readonly double _decayRatio;

			public DecayingIntegralPID(double kP, double kI, double kD, double timeStep, double decayRatio) : base(kP, kI, kD, timeStep)
			{
				_decayRatio = decayRatio;
			}

			protected override double GetIntegral(double currentError, double errorSum, double timeStep)
			{
                return errorSum * (1.0 - _decayRatio) + currentError * timeStep;
			}
		}

		public class ClampedIntegralPID : PID
		{
			readonly double _upperBound;
			readonly double _lowerBound;

			public ClampedIntegralPID(double kP, double kI, double kD, double timeStep, double lowerBound, double upperBound) : base(kP, kI, kD, timeStep)
			{
				_upperBound = upperBound;
				_lowerBound = lowerBound;
			}

			protected override double GetIntegral(double currentError, double errorSum, double timeStep)
			{
				errorSum += currentError * timeStep;
				return Math.Min(_upperBound, Math.Max(errorSum, _lowerBound));
			}
		}

		public class BufferedIntegralPID : PID
		{
			readonly Queue<double> _integralBuffer = new Queue<double>();
			readonly int _bufferSize = 0;

			public BufferedIntegralPID(double kP, double kI, double kD, double timeStep, int bufferSize) : base(kP, kI, kD, timeStep)
			{
				_bufferSize = bufferSize;
			}

			protected override double GetIntegral(double currentError, double errorSum, double timeStep)
			{
				if (_integralBuffer.Count == _bufferSize)
					_integralBuffer.Dequeue();
				_integralBuffer.Enqueue(currentError * timeStep);
				return _integralBuffer.Sum();
			}
		}

		#endregion

		// LAG CLASS BY D1R4G0N, THANK YOU!
		public class Lag
		{
			public double Value { get; private set; }
			public double Current { get; private set; }

			bool accurate;
            readonly double[] times;
			double sum = 0;
			int pos = 0;

			public Lag(int samples)
			{
				times = new double[samples];
			}

			public void Update(double time)
			{
				Current = time;
				sum -= times[pos];
				times[pos] = time;
				sum += time;
				pos++;
				if (pos == times.Length)
				{
					pos = 0;
					accurate = true;
				}
				Value = accurate ? sum / times.Length : sum / pos;
			}
		}
	}
}
