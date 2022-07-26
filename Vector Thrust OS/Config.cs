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
		// ------- Default configs --------
		string myName = "VT";
		double TimeForRefresh = 10;
		bool ShowMetrics = false;
		int SkipFrames = 0;


		double RotorStMultiplier = 1000;
		//bool SlowThrustOff = false;
		double MaxThrustOffRPM = 30;
		double lowThrustCutOn = 0.5;
		double lowThrustCutOff = 0.01;
		double lowThrustCutCruiseOn = 1;
		double lowThrustCutCruiseOff = 0.15;

		double[] Accelerations = new double[] { 0, 3.7, 5.22 };
		int gear = 0;
		double gearaccel = 0;

		bool TurnOffThrustersOnPark = true;
		bool RechargeOnPark = true;
		string BackupSubstring = ":Backup";
		bool PerformanceWhilePark = false;
		bool AutoAddGridConnectors = false;
		bool AutoAddGridLandingGears = false;
		//bool UsePIDPark = true;

		double thrustModifierAboveSpace = 0.01;
		double thrustModifierBelowSpace = 0.01;

		double thrustModifierAboveGrav = 0.01;
		double thrustModifierBelowGrav = 0.01;

		int RotationAverageSamples = 1;
		string[] tagSurround = new string[] { "|", "|" };
		//bool UsePID = false;
		bool cruisePlane = false; // make cruise mode act more like an airplane
		int FramesBetweenActions = 1;

		// ------- End default configs ---------


		// START CONFIG STRINGS AND VARS
		readonly MyIni config = new MyIni();
		readonly MyIni configCD = new MyIni();

		const string inistr = "--------Vector Thrust 2 Settings--------";

		const string detectstr = "Vector Thruster Stabilization";
		const string accelstr = "Acceleration Settings";
		const string parkstr = "Parking Settings";
		const string miscstr = "MISC";

		const string myNameStr = "Name Tag";
		const string TagSurroundStr = "Tag Surround Char(s)";
		const string ShowMetricsStr = "Show Metrics";
		const string TimeForRefreshStr = "Time For Each Refresh";
		const string SkipFramesStr = "Skip Frames";


		const string RotorStMultiplierStr = "Rotor Stabilization Multiplier";
		const string lowThrustCutStr = "Calculated Velocity To Turn On/Off VectorThrusters";
		const string lowThrustCutCruiseStr = "Calculated Velocity To Turn On/Off VectorThrusters In Cruise";
		//const string SlowThrustOffStr = "Slow Reposition Of Rotors On Turn Off";
		const string MaxThrustOffRPMStr = "Max Rotor RPM On Turn Off";

		const string AccelerationsStr = "Accelerations";
		const string gearStr = "Starting Acceleration Position";

		const string TurnOffThrustersOnParkStr = "Turn Off Thrusters On Park";
		const string RechargeOnParkStr = "Set Batteries/Tanks to Recharge/Stockpile On Park";
		const string BackupSubstringStr = "Assign Backup Batteries With SubTag";
		const string PerformanceWhileParkStr = "Run Script Each 100 Frames When Parked";
		const string AutoAddGridConnectorsStr = "Add Automatically Same Grid Connectors";
		const string AutoAddGridLandingGearsStr = "Add Automatically Same Grid Landing Gears";
		//const string UsePIDParkStr = "Use PID Controller to Handle Parking";

		const string thrustModifierSpaceStr = "Thruster Modifier Turn On/Off Space";
		const string thrustModifierGravStr = "Thruster Modifier Turn On/Off Gravity";

		const string RotationAverageSamplesStr = "Rotor Velocity Average Samples";
		//const string UsePIDStr = "Use PID Controller";
		const string cruisePlaneStr = "Cruise Mode Act Like Plane";
		const string FramesBetweenActionsStr = "Frames Per Operation: Block Assigner";

		// END STRINGS AND VARS

		void Config()
		{
			config.Clear();
			double[] defaultltc = new double[] { 0.5, 0.01 };
			double[] defaultltcc = new double[] { 1, 0.15 };
			double[] defaultacc = new double[] { 0, 3.7, 5.22 };
			double[] defaulttms = new double[] { 0.1, 0.1 };
			double[] defaulttmg = new double[] { 0.1, 0.1 };

			bool force = false;
			KeepConfig();

			if (config.TryParse(Me.CustomData))
			{
				myName = config.Get(inistr, myNameStr).ToString(myName);
				if (string.IsNullOrEmpty(myName))
				{
					myName = "VT";
					force = true;
				}
				textSurfaceKeyword = $"{myName}:";
				LCDName = $"{myName}LCD";

				string sstr = config.Get(inistr, TagSurroundStr).ToString();
				int sstrl = sstr.Length;

				if (sstrl == 1)
				{
					tagSurround = new string[] { sstr, sstr };
				}
				else if (sstrl > 1 && sstrl % 2 == 0)
				{
					string first = sstr.Substring(0, (int)(sstr.Length / 2));
					string last = sstr.Substring((int)(sstr.Length / 2), (int)(sstr.Length / 2));
					tagSurround = new string[] { first, last };
				}
				else
				{
					force = true;
					tagSurround = new string[] { "|", "|" };
				}

				TimeForRefresh = config.Get(inistr, TimeForRefreshStr).ToDouble(TimeForRefresh);
				ShowMetrics = config.Get(inistr, ShowMetricsStr).ToBoolean(ShowMetrics);
				SkipFrames = config.Get(inistr, SkipFramesStr).ToInt32(SkipFrames);

				RotorStMultiplier = config.Get(detectstr, RotorStMultiplierStr).ToDouble(RotorStMultiplier);
				//SlowThrustOff = config.Get(detectstr, SlowThrustOffStr).ToBoolean(SlowThrustOff);
				MaxThrustOffRPM = config.Get(detectstr, MaxThrustOffRPMStr).ToDouble(MaxThrustOffRPM);
				string temp = config.Get(detectstr, lowThrustCutStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaultltc; }
					lowThrustCutOn = result[0];
					lowThrustCutOff = result[1];
				}
				catch
				{
					force = true;
					lowThrustCutOn = defaultltc[0];
					lowThrustCutOff = defaultltc[1];
				}

				temp = config.Get(detectstr, lowThrustCutCruiseStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaultltcc; }
					lowThrustCutCruiseOn = result[0];
					lowThrustCutCruiseOff = result[1];
				}
				catch
				{
					force = true;
					lowThrustCutCruiseOn = defaultltcc[0];
					lowThrustCutCruiseOff = defaultltcc[1];
				}

				temp = config.Get(accelstr, AccelerationsStr).ToString();
				try
				{
					Accelerations = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (Accelerations.Length < 2) { force = true; Accelerations = defaultacc; }
				}
				catch { force = true; Accelerations = defaultacc; }

				if (justCompiled)
				{
					int geardef = config.Get(accelstr, gearStr).ToInt32();
					gear = geardef > Accelerations.Length - 1 ? gear : geardef;
				}

				TurnOffThrustersOnPark = config.Get(parkstr, TurnOffThrustersOnParkStr).ToBoolean(TurnOffThrustersOnPark);
				RechargeOnPark = config.Get(parkstr, RechargeOnParkStr).ToBoolean(RechargeOnPark);
				BackupSubstring = config.Get(parkstr, BackupSubstringStr).ToString(BackupSubstring);
				PerformanceWhilePark = config.Get(parkstr, PerformanceWhileParkStr).ToBoolean(PerformanceWhilePark);
				AutoAddGridConnectors = config.Get(parkstr, AutoAddGridConnectorsStr).ToBoolean(AutoAddGridConnectors);
				AutoAddGridLandingGears = config.Get(parkstr, AutoAddGridLandingGearsStr).ToBoolean(AutoAddGridLandingGears);
				//UsePIDPark = config.Get(parkstr, UsePIDParkStr).ToBoolean(UsePIDPark);

				temp = config.Get(miscstr, thrustModifierSpaceStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaulttms; }
					thrustModifierAboveSpace = result[0];
					thrustModifierBelowSpace = result[1];
				}
				catch
				{
					force = true;
					thrustModifierAboveSpace = defaulttms[0];
					thrustModifierBelowSpace = defaulttms[1];
				}

				temp = config.Get(miscstr, thrustModifierGravStr).ToString();
				try
				{
					double[] result = Array.ConvertAll(temp.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), s => double.Parse(s));
					if (result.Length != 2) { force = true; result = defaulttmg; }
					thrustModifierAboveGrav = result[0];
					thrustModifierBelowGrav = result[1];
				}
				catch
				{
					force = true;
					thrustModifierAboveGrav = defaulttmg[0];
					thrustModifierBelowGrav = defaulttmg[1];
				}
				RotationAverageSamples = config.Get(miscstr, RotationAverageSamplesStr).ToInt32(RotationAverageSamples);

				//UsePID = config.Get(miscstr, UsePIDStr).ToBoolean(UsePID);
				cruisePlane = config.Get(miscstr, cruisePlaneStr).ToBoolean(cruisePlane);
				FramesBetweenActions = config.Get(miscstr, FramesBetweenActionsStr).ToInt32(FramesBetweenActions);
				if (FramesBetweenActions <= 0)
				{
					FramesBetweenActions = 1;
					force = true;
				}
				timepause = FramesBetweenActions * timeperframe;
			}

			SetConfig(force);
			RConfig(config.ToString(), force);
		}

		double timepause = 0;
		void SetConfig(bool force)
		{

			double[] defaultltc = new double[] { 0.5, 0.01 };
			double[] defaultltcc = new double[] { 1, 0.15 };
			double[] defaultacc = new double[] { 0, 3.7, 5.22 };
			double[] defaulttms = new double[] { 0.1, 0.1 };
			double[] defaulttmg = new double[] { 0.1, 0.1 };

			config.Set(inistr, myNameStr, myName);
			string sstr = tagSurround[0].Equals(tagSurround[1]) ? tagSurround[0] : tagSurround[0] + tagSurround[1];
			config.Set(inistr, TagSurroundStr, sstr);
			config.Set(inistr, TimeForRefreshStr, TimeForRefresh);
			config.Set(inistr, ShowMetricsStr, ShowMetrics);
			config.Set(inistr, SkipFramesStr, SkipFrames);

			config.Set(detectstr, RotorStMultiplierStr, RotorStMultiplier);
			//config.Set(detectstr, SlowThrustOffStr, SlowThrustOff);
			config.Set(detectstr, MaxThrustOffRPMStr, MaxThrustOffRPM);
			string ltcstr = String.Join(",", force ? defaultltc : new double[] { lowThrustCutOn, lowThrustCutOff });
			config.Set(detectstr, lowThrustCutStr, ltcstr);
			string ltccstr = String.Join(",", force ? defaultltcc : new double[] { lowThrustCutCruiseOn, lowThrustCutCruiseOff });
			config.Set(detectstr, lowThrustCutCruiseStr, ltccstr);

			string accstr = String.Join(",", force ? defaultacc : Accelerations);
			config.Set(accelstr, AccelerationsStr, accstr);
			if (justCompiled) config.Set(accelstr, gearStr, gear);

			config.Set(parkstr, TurnOffThrustersOnParkStr, TurnOffThrustersOnPark);
			config.Set(parkstr, RechargeOnParkStr, RechargeOnPark);
			config.Set(parkstr, BackupSubstringStr, BackupSubstring);
			config.Set(parkstr, PerformanceWhileParkStr, PerformanceWhilePark);
			config.Set(parkstr, AutoAddGridConnectorsStr, AutoAddGridConnectors);
			config.Set(parkstr, AutoAddGridLandingGearsStr, AutoAddGridLandingGears);
			//config.Set(parkstr, UsePIDParkStr, UsePIDPark);

			string tmsstr = String.Join(",", force ? defaulttms : new double[] { thrustModifierAboveSpace, thrustModifierBelowSpace });
			string tmgstr = String.Join(",", force ? defaulttms : new double[] { thrustModifierAboveGrav, thrustModifierBelowGrav });

			config.Set(miscstr, thrustModifierSpaceStr, tmsstr);
			config.Set(miscstr, thrustModifierGravStr, tmgstr);
			config.SetComment(miscstr, thrustModifierSpaceStr, "\n-Thruster Modifier-\nHow far needs the thruster to turn on and off from desired angle.\n Space:");
			config.SetComment(miscstr, thrustModifierGravStr, "\n Gravity:");
			config.Set(miscstr, RotationAverageSamplesStr, RotationAverageSamples);
			//config.Set(miscstr, UsePIDStr, UsePID);
			config.Set(miscstr, cruisePlaneStr, cruisePlane);
			config.Set(miscstr, FramesBetweenActionsStr, FramesBetweenActions);
		}

		void RConfig(string output, bool force = false)
		{
			if (force || output != Me.CustomData) Me.CustomData = output;
			try { if (!force && !Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData = Me.CustomData.Replace(Me.CustomData.Between("\n---\n", "0")[0], textSurfaceKeyword); }
			catch { if (!justCompiled) log.AppendNR("No tag found textSufaceKeyword\n"); }
			if (!force && !Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData += $"\n---\n{textSurfaceKeyword}0";
		}

		void KeepConfig()
		{
			if (justCompiled && configCD.TryParse(Me.CustomData))
			{
				SetConfig(false);
				List<MyIniKey> ccd = new List<MyIniKey>();
				List<MyIniKey> ccfg = new List<MyIniKey>();

				configCD.GetKeys(ccd);
				config.GetKeys(ccfg);

				foreach (MyIniKey cd in ccd)
				{
					foreach (MyIniKey cfg in ccfg)
					{
						if (cd.Equals(cfg)) config.Set(cfg, configCD.Get(cd).ToString());
					}
				}
				RConfig(config.ToString(), true);
			}
			configCD.Clear();
		}

		// END ENTIRE CONFIG HANDLER

	}
}
