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
		// Thanks to Digi for creating this example class
		class SimpleTimerSM
		{
			public readonly Program Program;
			public bool AutoStart { get; set; }
			public bool Running { get; private set; }
			public IEnumerable<double> Sequence;
			public double SequenceTimer { get; private set; }

			private IEnumerator<double> sequenceSM;

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


		public IEnumerable<double> MainTagSeq()
		{
			while (true) {
				log = new StringBuilder(" >Checking Vector Thrusters\n");
				if (!checkVectorThrusters())
				{
					log.AppendLine(" Something went wrong! Stopping Script.\n");
					ManageTag(true);
					shutdown = true;
					yield return TimeBetweenAction;
				}
				if (!justCompiled) yield return TimeBetweenAction;
				string akshan = docheck ? "Checking" : "Skipping";
				log = new StringBuilder($" >{akshan} Controllers\n");
				if (docheck) getControllers();
				if (!justCompiled) yield return TimeBetweenAction;
				log = new StringBuilder($" >{akshan} Screens\n");
				if (docheck) getScreens();
				yield return TimeBetweenAction;
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
			double _lastError = 0;
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
				return errorSum = errorSum * (1.0 - _decayRatio) + currentError * timeStep;
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
				errorSum = errorSum + currentError * timeStep;
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
			double[] times;
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
