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


		public IEnumerable<double> GetScreensSeq()
		{
			while (true) {
				if (check) log.AppendNR($"  Getting Screens => new:{input_screens.Count}\n");
				//bool greedy = this.greedy || this.applyTags || this.removeTags; //deprecated
				if (input_screens.Any()) {
					this.screens.AddRange(input_screens);
					LND(ref screens); // TODO: Check if this is worth dealing with (It can be)
					input_screens.Clear();
					if (pauseseq) yield return timepause;
				}


				//LND(ref input_screens); //just in case this doesn't do anything at all

				if (Me.SurfaceCount > 0)
				{
					surfaceProviderErrorStr = "";
					AddSurfaceProvider(Me);
					Me.GetSurface(0).FontSize = 2.2f;
					// this isn't really the right place to put this, but doing it right would be a lot more code (moved here temporarily)
				}

				foreach (IMyTextPanel screen in this.screens)
				{
					bool cond1 = surfaces.Contains(screen);
					bool cond2 = screen.IsWorking;
					bool cond3 = screen.CustomName.ToLower().Contains(LCDName.ToLower());
					bool cond4 = screen.Closed;

					if (!cond1 && cond2 && cond3) surfaces.Add(screen);
					else if (cond1 && (!cond2 || !cond3 || cond4)) surfaces.Remove(screen);

					if (pauseseq) yield return timepause;
				}

				//screenCount = screens.Count;
				if (check) {
					if (pauseseq) yield return timepause;
					log.AppendNR($"  ->Done. Total Screens {screens.Count} => Total Surfaces:{surfaces.Count}\n");
					LND(ref surfaces); //just in case}
				}
				GetScreen.Doneloop = true;
				yield return timepause;
			}
		}

		void GenerateProgressBar(bool perf = false) {
			double percent = gearaccel / maxaccel;
			if (!perf) {
				progressbar.Clear();
				progressbar.ProgressBar(percent, 30);
			}
			trueaccel = $" ({(percent * totaleffectivethrust).Round(2)} {{m/s^2}}) ";
		}


		public void Print(params object[] args)
		{
			string separator = args[0].ToString();
			bool writes = false;
			if (separator.Contains("%sep%")) separator = separator.Replace("%sep%", "");
			else separator = " - ";
			StringBuilder result = new StringBuilder();

			foreach (object arg in args)
			{
				string arg_s = arg.ToString();
				if (arg_s.Contains("%scr%")) writes = true;
				else if (!arg_s.Contains("%sep%")) result.Append(arg_s).Append(separator);
			}

			log.AppendNR(result.ToString());
			if (writes) screensb.Append(result);
		}


		IEnumerable<double> GetControllersSeq()
		{
			while (true) {
				bool greedy = this.greedy || this.applyTags;// || this.removeTags;

				if (this.controllers_input.Count > 0) {
					this.controllers.AddRange(controllers_input);
					LND(ref controllers);
					controllers_input.Clear();
					if (pauseseq) yield return timepause;
				}

				//LND(ref controllers_input); doesn't do anything

				StringBuilder reason = new StringBuilder();
				foreach (ShipController s in this.controllers)
				{
					bool canAdd = true;
					StringBuilder currreason = new StringBuilder(s.TheBlock.CustomName + "\n");
					if (!s.TheBlock.ShowInTerminal && ignoreHiddenBlocks)
					{
						currreason.AppendLine("  ShowInTerminal not set\n");
						canAdd = false;
					}
					if (!s.TheBlock.CanControlShip)
					{
						currreason.AppendLine("  CanControlShip not set\n");
						canAdd = false;
					}
					if (!s.TheBlock.ControlThrusters)
					{
						currreason.AppendLine("  Can't ControlThrusters\n");
						canAdd = false;
					}
					/*if (s.theBlock.IsMainCockpit)
					{ // I thiink this could make problems in the future
						mainController = s;
					}*/
					if (!greedy && !HasTag(s.TheBlock))
					{
						currreason.AppendLine("  Doesn't match my tag\n");
						canAdd = false;
					}
					/*if (this.removeTags)
					{
						RemoveTag(s.TheBlock);
					}*/

					if (canAdd)
					{
						AddSurfaceProvider(s.TheBlock); // TODO, THIS ONLY DETECTS COCKPITS
						s.Dampener = s.TheBlock.DampenersOverride;
						if (!controlledControllers.Contains(s))
						{
							controlledControllers.Add(s);
							ccontrollerblocks.Add(s.TheBlock);
						}
						//mainController = s; //temporal
						/*if (s.theBlock.IsUnderControl)
						{
							controlledControllers.Add(s);
						}*/

						if (this.applyTags)
						{
							AddTag(s.TheBlock);
						}
						if (pauseseq) yield return timepause;
					}
					else
					{
						RemoveSurfaceProvider(s.TheBlock);
						if (controlledControllers.Contains(s))
						{
							controlledControllers.Remove(s);
							ccontrollerblocks.Remove(s.TheBlock);
						}
						reason.Append(currreason);
					}
				}

				//print("conts", controllers.Count, controllerblocks.Count, controlledControllers.Count, ccontrollerblocks.Count);
				if (pauseseq) yield return timepause;

				if (controllers.Count == 0) reason.AppendLine("Any Controller Found.\nEither for missing tag, not working or removed.");
				if (controlledControllers.Count == 0)
				{
					log.AppendNR("ERROR: no usable ship controller found. Reason: \n");
					log.AppendNR(reason.ToString());
					ManageTag(true);
					yield return timepause;
				}
				else if (controlledControllers.Count > 0)
				{
					foreach (ShipController s in controlledControllers)
					{
						if (s.TheBlock.IsUnderControl)
						{
							mainController = s;
							break;
						}
						if (pauseseq) yield return timepause;
					}
					if (mainController == null)
					{
						mainController = controlledControllers[0];
					}
				}

				GetControllers.Doneloop = true;
				yield return timepause;
			}
		}

		void LND<T>(ref List<T> obj) {
			obj = obj.Distinct().ToList();
		}

		public IEnumerable<double> GetVectorThrustersSeq()
		{
			while (true) {
				bool greedy = this.applyTags /*|| this.removeTags*/ || this.greedy;

				log.AppendNR("  >Getting Rotors\n");
				// make this.nacelles out of all valid rotors
				foreach (IMyTerminalBlock r in vtrotors) {
					/*if (this.removeTags)
					{
						RemoveTag(r);
					}
					else*/
					if (this.applyTags)
					{
						AddTag(r);
					}
					if (pauseseq) yield return timepause;
				}

				foreach (IMyTerminalBlock tr in vtthrusters)
				{
					/*if (this.removeTags)
					{
						RemoveTag(tr);
					}
					else*/
					if (this.applyTags)
					{
						//log.AppendNR("Applying: " + tr.CustomName + "\n");
						AddTag(tr);
					}
					if (pauseseq) yield return timepause;
				}

				//print("%sep%\n", "abandoned:", abandonedrotors.Count, abandonedthrusters.Count);

				rotors_input.AddRange(abandonedrotors);
				if (pauseseq) yield return timepause;
				thrusters_input.AddRange(abandonedthrusters);
				if (pauseseq) yield return timepause;
				LND(ref rotors_input);
				if (pauseseq) yield return timepause;
				LND(ref thrusters_input);
				if (pauseseq) yield return timepause;

				foreach (IMyMotorStator current in rotors_input)
				{
					/*if (this.removeTags)
					{
						RemoveTag(current);
					}
					else */
					if (this.applyTags)
					{
						AddTag(current);
					}

					//log.AppendNR("RT:" + current.CustomName + "/" + (!greedy && HasTag(current)));k
					//bool cond = GridTerminalSystem.CanAccess(current) && !current.Closed && current.IsWorking && current.IsAlive();

					if (/*cond &&*/ current.Top != null && (greedy || HasTag(current)) && current.TopGrid != Me.CubeGrid)
					{
						Rotor rotor = new Rotor(current, this);
						this.vectorthrusters.Add(new VectorThrust(rotor, this));
						vtrotors.Add(current);
					}
					else {
						RemoveTag(current);
					}
					if (pauseseq) yield return timepause;
				}

				log.AppendNR("  >Getting Thrusters\n");
				// add all thrusters to their corrisponding nacelle and remove this.nacelles that have none
				for (int i = this.vectorthrusters.Count - 1; i >= 0; i--)
				{
					IMyMotorStator temprotor = this.vectorthrusters[i].rotor.TheBlock;
					//if (!vtrotors.Contains(this.vectorthrusters[i].rotor.theBlock)) this will cause problems
					for (int j = thrusters_input.Count - 1; j >= 0; j--)
					{
						bool added = false;


						if (greedy || HasTag(thrusters_input[j])) {
							/*if (this.removeTags)
							{
								RemoveTag(thrusters_input[j]);
							}*/

							bool cond = thrusters_input[j].CubeGrid == this.vectorthrusters[i].rotor.TheBlock.TopGrid;
							//bool cond2 = this.vectorthrusters[i].thrusters.Any(x => vtthrusters.Any(y => y == x));
							bool cond2 = vectorthrusters[i].thrusters.Any(x => x.TheBlock == thrusters_input[j]);

							/*bool cond3 = false;
							Print("%sep%\n", thrusters_input[j].CustomName, cond2);
							foreach (Thruster t in vectorthrusters[i].thrusters) {
								if (t.TheBlock == thrusters_input[j]) {
									Print("%sep%\n", t.TheBlock.CustomName, thrusters_input[j].CustomName);
									cond3 = true;
								}
							}
							log.AppendNR("c:" + cond3);*/

							// thruster is not for the current nacelle
							// if(!thrusters[j].IsFunctional) continue;// broken, don't add it

							if (cond && this.applyTags)
							{
								AddTag(thrusters_input[j]);
							}
							//if (this.vectorthrusters[i].thrusters.Any(x => vtthrusters.Contains(x.theBlock))) continue;
							//doesn't add it if it already exists


							if (cond && !cond2)
							{
								if (justCompiled)
								{
									thrusters_input[j].ThrustOverridePercentage = 0;
									thrusters_input[j].Enabled = true;
								}

								added = true;
								abandonedthrusters.Remove(thrusters_input[j]);
								this.vectorthrusters[i].thrusters.Add(new Thruster(thrusters_input[j]));
								vtthrusters.Add(thrusters_input[j]);
								thrusters_input.RemoveAt(j);// shorten the list we have to check (It discards thrusters for next nacelle)
							}
						}

						if (!added/* && !FilterThis(thrusters_input[i])*/ && !abandonedthrusters.Contains(thrusters_input[j]))
							//vtthrusters.Remove(thrusters_input[j]);
							abandonedthrusters.Add(thrusters_input[j]);
						if (pauseseq) yield return timepause;
					}

					// remove this.nacelles (rotors) without thrusters
					if (this.vectorthrusters[i].thrusters.Count == 0/* || this.vectorthrusters[i].rotor.TheBlock.Top == null*/)
					{
						//log.AppendNR("RT:" + temprotor.CustomName + "/" + (!greedy && HasTag(temprotor)));
						/*if (justCompiled) //idk why I put this here
						{
							temprotor.Brake(); //temprotor.TargetVelocityRPM = 0;
							temprotor.RotorLock = false;
							temprotor.Enabled = true;
						}*/
						if (!abandonedrotors.Contains(temprotor)) abandonedrotors.Add(temprotor);
						vtrotors.Remove(temprotor);
						RemoveTag(temprotor);
						this.vectorthrusters.RemoveAt(i);// there is no more reference to the rotor, should be garbage collected (NOT ANYMORE, Added to abandoned rotors)
					}
					else {
						// if its still there, setup the nacelle
						if (justCompiled)
						{
							temprotor.Brake(); //temprotor.TargetVelocityRPM = 0;
							temprotor.RotorLock = false;
							temprotor.Enabled = true;
						}

						abandonedrotors.Remove(temprotor);
						this.vectorthrusters[i].ValidateThrusters();
						this.vectorthrusters[i].DetectThrustDirection();
						this.vectorthrusters[i].AssignGroup();
						//AddToGroup(this.vectorthrusters[i]);
					}
					if (pauseseq) yield return timepause;
				}

				log.AppendNR("  >Grouping VTThrs\n");
				//GroupVectorThrusters();

				if (VTThrGroups.Count == 0)
				{
					log.AppendNR("  > [ERROR] => Any Vector Thrusters Found!\n");
					error = true;
					ManageTag(true);
					if (pauseseq) yield return timepause;
				}
				thrusters_input.Clear();
				rotors_input.Clear();
				GetVectorThrusters.Doneloop = true;
				yield return timepause;
			}
		}


		void CheckWeight() {
			ShipController cont = FindACockpit();
			/*if (check && cont != null) { //This caused checking of blocks all the time
				myshipmass = cont.TheBlock.CalculateShipMass();

				if (this.oldMass != myshipmass.BaseMass) {
					log.AppendNR("New weight encountered, checking again\n");
					ResetVTHandlers();
				}
				//if (!justCompiled) GenerateProgressBar(true);
				this.oldMass = myshipmass.BaseMass;
				return; 
			}*/
			if (cont == null)
			{
				log.AppendNR("  -No cockpit registered, checking mainController\n");
				if (!GridTerminalSystem.CanAccess(mainController.TheBlock)) {
					mainController = null;
					foreach (ShipController c in controlledControllers) {
						if (GridTerminalSystem.CanAccess(c.TheBlock)) {
							mainController = c;
							break;
						}
					}
				}
				if (mainController == null) {
					error = true;
					log.AppendNR("ERROR, ANY CONTROLLERS FOUND - SHUTTING DOWN");
					ManageTag(true);
					return;
				}
			}
			else if (!applyTags)
			{
				myshipmass = cont.TheBlock.CalculateShipMass();
				float bm = myshipmass.BaseMass;
				//log.AppendNR("mass:" + bm);

				if (bm < 0.001f)
				{
					log.AppendNR("  -Can't fly a Station\n");
					isstation = true;
					Runtime.UpdateFrequency = UpdateFrequency.Update100;
					return;
				}
				else if (isstation)
				{
					isstation = false;
					Runtime.UpdateFrequency = update_frequency;
				}
				if (this.oldMass == bm) return; //modifying variables here may cause to the handler to restart every single time
												//check = false;
												//log.AppendNR("mass not dif");
												//log.AppendNR("mass dif");
				/*if (check) {
					log.AppendNR("repeating");
					OneRunMainChecker(false);
				}*/
				this.oldMass = bm; //else:
			}
			//if (!check) THIS CAUSES PROBLEMS
			OneRunMainChecker(false);
			if (!justCompiled) GenerateProgressBar(true);
		}


		public IEnumerable<double> CheckParkBlocksSeq() { //this is executed only if there's not new mass
			while (true) {
				for (int i = normalbats.Count - 1; i >= 0; i--)
				{
					IMyBatteryBlock b = normalbats[i];
					if (HasTag(b)) {
						log.AppendNR($"Filtered Bat: {b.CustomName}");
						normalbats.RemoveAt(i);
						if (!taggedbats.Contains(b)) taggedbats.Add(b);
					}
					yield return timepause;
				}

				for (int i = taggedbats.Count - 1; i >= 0; i--)
				{
					IMyBatteryBlock b = taggedbats[i];
					if (!HasTag(b)) {
						log.AppendNR($"Filtered TagBat: {b.CustomName}");
						taggedbats.RemoveAt(i);
						if (FilterThis(b) && !normalbats.Contains(b)) normalbats.Add(b);
					}
					yield return timepause;
				}

				for (int i = connectors.Count - 1; i >= 0; i--)
				{
					IMyShipConnector c = connectors[i];
					bool hastag = HasTag(c);
					if ((ConnectorNeedsSuffixToPark && !hastag) || (!ConnectorNeedsSuffixToPark && (hastag || !FilterThis(c)))) {
						log.AppendNR($"Filtered Con: {c.CustomName}");
						connectors.RemoveAt(i);
					}
					yield return timepause;
				}

				for (int i = landinggears.Count - 1; i >= 0; i--)
				{
					IMyLandingGear l = landinggears[i];
					if ((AutoAddGridLandingGears && !HasTag(l) && !FilterThis(l)) || (!AutoAddGridLandingGears && !HasTag(l))) {
						log.AppendNR($"Filtered LanGear: {l.CustomName}");
						landinggears.RemoveAt(i);
					}
					yield return timepause;
				}

				CheckParkBlocks.Doneloop = true;
				yield return timepause;
			}
			
		}

		public IEnumerable<double> CheckVectorThrustersSeq()
		{
			while (true) {
				pauseseq = ((!justCompiled || (justCompiled && error)) && !applyTags);
				if (pauseseq) yield return timepause;
				if (!check) {
					//log.AppendNR("  -Mass is the same.\n");

					/*print("%sep%\n", vectorthrusters.Count, vtrotors.Count, abandonedrotors.Count, vtthrusters.Count, abandonedthrusters.Count);
					foreach (VectorThrust vt in vectorthrusters) {
						print("%sep%\n", vt.rotor.CName, vt.thrusters.Count);
					}*/

					while (!GetControllers.Doneloop)
					{
						GetControllers.Run();
						yield return timepause;
					}
					GetControllers.Doneloop = false;

					while (!GetScreen.Doneloop)
					{
						GetScreen.Run();
						yield return timepause;
					}
					GetScreen.Doneloop = false;

					while (!CheckParkBlocks.Doneloop) 
					{
						CheckParkBlocks.Run();
						yield return timepause;
					}
					CheckParkBlocks.Doneloop = false;

					log.AppendNR(" -Everything seems normal.");
					continue;
				}

				if (!justCompiled) log.AppendNR("  -Mass is different, checking everything\n");

				List<IMyTerminalBlock> allblocks = new List<IMyTerminalBlock>(connectors);
				allblocks = allblocks
							.Concat(landinggears)
							.Concat(tankblocks)
							.Concat(taggedbats)
							.Concat(normalbats) //insead of gridbats
							//.Concat(remainingbats) 
							.Concat(cruiseThr)
							.Concat(normalThrusters)
							.Concat(controllerblocks)
							.Concat(ccontrollerblocks)
							.Concat(vtrotors)
							.Concat(abandonedrotors)
							.Concat(abandonedthrusters) // to remove deleted ones, don't panic if you don't find this variable anywhere
							.Concat(vtthrusters)
							.Concat(screens)
							.ToList();

				if (pauseseq) yield return timepause;
				int oldNThrC = normalThrusters.Count;

				foreach (IMyTerminalBlock b in allblocks) {

					bool tagallcond = TagAll && (b is IMyBatteryBlock || b is IMyGasTank || b is IMyLandingGear || b is IMyShipConnector);
					bool tagcond = b is IMyShipController || vtthrusters.Contains(b) || b is IMyMotorStator;

					if (!GridTerminalSystem.CanAccess(b))
					{
						if (b is IMyLandingGear || b is IMyShipConnector)
						{
							if (b is IMyShipConnector) connectors.Remove((IMyShipConnector)b);
							else landinggears.Remove((IMyLandingGear)b);
						}
						else if (b is IMyGasTank)
						{
							tankblocks.Remove((IMyGasTank)b);
						}
						else if (b is IMyBatteryBlock)
						{
							taggedbats.Remove((IMyBatteryBlock)b);
							normalbats.Remove((IMyBatteryBlock)b);
							//if (FilterThis(b)) gridbats.Remove(b);
						}
						else if (b is IMyMotorStator)
						{
							abandonedrotors.Remove((IMyMotorStator)b);
							vtrotors.Remove((IMyMotorStator)b);
						}
						else if (b is IMyThrust)
						{
							abandonedthrusters.Remove((IMyThrust)b);
							vtthrusters.Remove((IMyThrust)b);
							if (FilterThis(b))
							{
								cruiseThr.Remove((IMyThrust)b);
								normalThrusters.Remove((IMyThrust)b);
							}
						}
						else if (b is IMyShipController) {
							ccontrollerblocks.Remove((IMyShipController)b);
							controllerblocks.Remove((IMyShipController)b);
						}
						else if (b is IMyTextPanel)
						{
							screens.Remove((IMyTextPanel)b);
							RemoveSurface((IMyTextPanel)b);
						}

					}
					else if (applyTags && (tagallcond || tagcond)) {
						log.AppendNR("Adding tag:" + b.CustomName + "\n");
						AddTag(b);
					} else if (b is IMyMotorStator && (b as IMyMotorStator).Top == null) {
						log.AppendNR("NO TOP: " + b.CustomName + "\n");
						RemoveTag(b);
						abandonedrotors.Remove((IMyMotorStator)b);
						vtrotors.Remove((IMyMotorStator)b);
					}
					if (pauseseq) yield return timepause;
				}

				int NThrC = normalThrusters.Count;
				if (NThrC == 0 && NThrC != oldNThrC)
				{
					dampeners = true; //Put dampeners back on if normalthrusters got removed entirely
				}

				controllers.RemoveAll(x => !GridTerminalSystem.CanAccess(x.TheBlock));
				if (pauseseq) yield return timepause;

				vectorthrusters.RemoveAll(x => !vtrotors.Contains(x.rotor.TheBlock));
				if (pauseseq) yield return timepause;

				foreach (VectorThrust vt in vectorthrusters) {
					vt.thrusters.RemoveAll(x => !vtthrusters.Contains(x.TheBlock));
					vt.activeThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
					vt.availableThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
					if (pauseseq) yield return timepause;
				}

				foreach (List<VectorThrust> group in VTThrGroups) {
					group.RemoveAll(x => !vectorthrusters.Contains(x) || x.thrusters.Count < 1);
					if (pauseseq) yield return timepause;
				}

				VTThrGroups.RemoveAll(x => x.Count < 1);
				if (pauseseq) yield return timepause;

				for (int i = controlledControllers.Count - 1; i >= 0; i--)
				{
					if (pauseseq) yield return timepause;
					if (!GridTerminalSystem.CanAccess(controlledControllers[i].TheBlock))
					{
						/*if (controlledControllers.Count == 1) {
							error = true;
							if (pauseseq) yield return timepause;
						}*/

						RemoveSurfaceProvider(controlledControllers[i].TheBlock);
						controlledControllers.RemoveAt(i);
					}
					if (pauseseq) yield return timepause;
				}

				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
				GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
				if (pauseseq) yield return timepause;

				blocks = blocks.Except(connectors)
					.Except(landinggears)
					.Except(tankblocks)
					.Except(taggedbats) //batteriesblocks
					.Except(normalbats) //backupbats
					//.Except(remainingbats) //batteries that the script won't touch (for now)
					.Except(cruiseThr)
					.Except(normalThrusters)
					.Except(ccontrollerblocks)
					.Except(controllerblocks)
					.Except(vtrotors)
					.Except(vtthrusters)
					.Except(screens)
					.Except(abandonedrotors)
					.Except(abandonedthrusters)
					.ToList();
				if (pauseseq) yield return timepause;

				


				//artificial scope (Removed)
				foreach (IMyTerminalBlock b in blocks)
				{

					bool island = b is IMyLandingGear;
					bool iscon = b is IMyShipConnector;
					bool samegrid = FilterThis(b);
					bool hastag = HasTag(b);

					if (b is IMyShipController)
					{
						controllerblocks.Add((IMyShipController)b);
						controllers_input.Add(new ShipController((IMyShipController)b));
					}
					else if (b is IMyMotorStator)
					{
						IMyMotorStator rt = (IMyMotorStator)b;
						/*if (justCompiled) //this stops ALL rotors even if they will not make part of the script
						{
							rt.TargetVelocityRPM = 0;
							rt.RotorLock = false;
							rt.Enabled = true;
						}*/
						rotors_input.Add(rt);

					}
					else if (b is IMyThrust)
					{
						IMyThrust tr = (IMyThrust)b;
						/*if (justCompiled) //this stops ALL thrusters override even if they will not make part of the script
						{
							tr.ThrustOverridePercentage = 0;
							tr.Enabled = true;
						}*/

						if (samegrid)
						{
							normalThrusters.Add((IMyThrust)b);
							if (b.Orientation.Forward == mainController.TheBlock.Orientation.Forward) //changing
							{
								cruiseThr.Add((IMyThrust)b);
								log.AppendNR("Added back thrust: " + b.CustomName);
							}
							(b as IMyFunctionalBlock).Enabled = true;
						}
						else {
							thrusters_input.Add(tr);
						}
					}
					/*if (b is IMyProgrammableBlock) // I will use it in the future maybe for autopilot
					{
						programBlocks.Add((IMyProgrammableBlock)b);
					}*/
					else if (b is IMyTextPanel)
					{
						input_screens.Add((IMyTextPanel)b);
					}

					else if ((iscon || island) && ((ConnectorNeedsSuffixToPark && hastag) || TagAll || (((!ConnectorNeedsSuffixToPark && !hastag) || (AutoAddGridLandingGears && island && samegrid)) && samegrid)))
					{
						if (TagAll) AddTag(b);
						if (iscon && !connectors.Contains(b)) connectors.Add((IMyShipConnector)b);
						else if (island && !landinggears.Contains(b)) landinggears.Add((IMyLandingGear)b);
					}
					else if (b is IMyGasTank && (hastag || TagAll || FilterThis(b)))
					{
						if (TagAll) AddTag(b);
						tankblocks.Add((IMyGasTank)b);
						if (hastag) (b as IMyGasTank).Stockpile = false;
					}
					else if (b is IMyBatteryBlock)
					{
						if (TagAll) AddTag(b);
						if (justCompiled && (hastag || samegrid)) (b as IMyBatteryBlock).ChargeMode = ChargeMode.Auto;

						if (hastag) taggedbats.Add((IMyBatteryBlock)b);
						else if (samegrid) normalbats.Add((IMyBatteryBlock)b);
						//else remainingbats.Add((IMyBatteryBlock)b);
					}
					if (pauseseq) yield return timepause;
				}
				// TODO: Compare if blocks are equal, or make other quick way to gather correct blocks (DONE)

				/*if (!taggedbats.Empty() && !normalbats.Empty()) {
					
				} else if (!taggedbats.Empty()) {
					backupbats.Add(normalbats[0]);

				}*/


				while (!GetControllers.Doneloop)
				{
					GetControllers.Run();
					if (pauseseq) yield return timepause;
				}
				GetControllers.Doneloop = false;

				while (!GetScreen.Doneloop)
				{
					GetScreen.Run();
					if (pauseseq) yield return timepause;
				}
				GetScreen.Doneloop = false;

				while (!GetVectorThrusters.Doneloop)
				{
					GetVectorThrusters.Run();
					if (pauseseq) yield return timepause;
				}
				GetVectorThrusters.Doneloop = false;

				LND(ref controllerblocks);
				if (pauseseq) yield return timepause;

				//LND(ref ccontrollerblocks); //not necessary, already applied in getcontrollersseq
				LND(ref vectorthrusters);
				if (pauseseq) yield return timepause;

				LND(ref normalThrusters);
				if (pauseseq) yield return timepause;

				//LND(ref controllers_input); already implemented in getcontrollerseq
				// TODO: Check if this is really necessary
				//LND(ref rechargedblocks); //I think this isn't
				//LND(ref turnedoffthusters); //I think this isn't
				//LND(ref backupbats); //I think this isn't
				//LND(ref surfaces);// Implemented in getscreensseq with screens too

				LND(ref vtthrusters);
				if (pauseseq) yield return timepause;

				LND(ref vtrotors);
				if (pauseseq) yield return timepause;

				check = false;
				yield return timepause;
			}
		}


		public IEnumerable<double> GetBatStatsSeq()
		{
			while (true)
			{
				outputbatsseq.Clear();
				if (batsseq.Count > 0)
				{
					double inputs = 0;
					double outputs = 0;
					double percents = 0;
					foreach (IMyPowerProducer b in batsseq)
					{
						//bool isb = b is IMyBatteryBlock;
						//if (b.ChargeMode != ChargeMode.Recharge) {//Nope, don't do this, it's useless
						outputs += b.CurrentOutput;
						if (b is IMyBatteryBlock)
						{
							inputs += (b as IMyBatteryBlock).CurrentInput;
							percents += (b as IMyBatteryBlock).CurrentStoredPower / (b as IMyBatteryBlock).MaxStoredPower;
						}
						
						outputs -= b.MaxOutput; 
						//}
						yield return timepause;
					}
					inputs /= inputs != 0 ? batsseq.Count : 1;
					outputs /= outputs != 0 ? batsseq.Count: 1;
					percents *= percents != 0 ? (100 / batsseq.Count) : 1;

					outputbatsseq = new List<double> { inputs, outputs, percents.Round(0) };
					yield return timepause;
				}
				BatteryStats.Doneloop = true;
				yield return timepause;
			}
		}

		

		public IEnumerable<double> BlockManagerSeq() {
			List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
			List<IMyBatteryBlock> backupbatteries = new List<IMyBatteryBlock>();
			bool setthr = false;
			bool donescan = false;
			//if (!parked) Runtime.UpdateFrequency = update_frequency; //Done in a betterway

			while (true) {
				bool turnoffthr = TurnOffThrustersOnPark && !normalThrusters.Empty();

				if (turnoffthr && (!setthr || !parked))
				{
					normalThrusters.ForEach(x => x.Enabled = !parked);
					setthr = true;
				}

				if ((normalbats.Count + taggedbats.Count < 2) || !RechargeOnPark) {
					if (!parked) alreadyparked = false;
					EndBM(true);
					yield return timepause;
					continue;
				}

				if (batteries.Empty() && backupbatteries.Empty()) {
					List<IMyBatteryBlock> allbats = new List<IMyBatteryBlock>(taggedbats).Concat(normalbats).ToList();
					if (parked) yield return timepause;
					backupbatteries = allbats.FindAll(x => x.CustomName.Contains(tag+BackupSubstring));
					if (parked) yield return timepause;

					if (!backupbatteries.Empty() && allbats.SequenceEqual(backupbatteries)/* && backupbatteries.Count == taggedbats.Count + normalbats.Count*/) {
						batteries = new List<IMyBatteryBlock>(backupbatteries);
						backupbatteries = new List<IMyBatteryBlock> { batteries[0] };
						batteries.RemoveAt(0);
					} else if (!backupbatteries.Empty()) {         
						batteries = allbats.Except(backupbatteries).ToList();
					}

					else if (backupbatteries.Empty() && taggedbats.Count > normalbats.Count)
					{
						backupbatteries = new List<IMyBatteryBlock>(normalbats);

						if (normalbats.Empty())
						{
							backupbatteries = new List<IMyBatteryBlock> { taggedbats[0] };
						}

						batteries = new List<IMyBatteryBlock>(taggedbats).Except(backupbatteries).ToList();
					}
					else if (backupbatteries.Empty())
					{
						backupbatteries.Add(normalbats.Empty() ? taggedbats[0] : normalbats[0]);
						batteries = batteries.Concat(normalbats).Concat(taggedbats).Except(backupbatteries).ToList();
					}
					//Getting at least 1 bat/tank to handle thrusters for a bit
					if (!parked) { 
						if (!batteries.Empty()) batteries[0].ChargeMode = ChargeMode.Auto;
						if (!tankblocks.Empty()) tankblocks[0].Stockpile = false;
					} 
					yield return timepause;
				}

				List<IMyPowerProducer> pw = new List<IMyPowerProducer>();
				GridTerminalSystem.GetBlocksOfType(pw, x => !batteries.Contains(x) && !backupbatteries.Contains(x));
				yield return timepause;

				List<double> statsBBATS = new List<double>();
				List<double> statsPW = new List<double>();
				//List<double> statsBATS = new List<double>();
				yield return timepause;

				if (!donescan || (BlockManager.Doneloop && parked)) {
					batsseq = new List<IMyTerminalBlock>(pw);
					while (!BatteryStats.Doneloop)
					{
						BatteryStats.Run();
						yield return timepause;
					}
					BatteryStats.Doneloop = false;

					donescan = true;
					statsPW = new List<double>(outputbatsseq);
					yield return timepause;

					batsseq = new List<IMyTerminalBlock>(backupbatteries);
					while (!BatteryStats.Doneloop)
					{
						BatteryStats.Run();
						yield return timepause;
					}
					BatteryStats.Doneloop = false;

					statsBBATS = new List<double>(outputbatsseq);
					yield return timepause;
				}

				bool lowbackupbat = !statsBBATS.Empty() && statsBBATS[2] < 2.5;
				bool comebackupbat = !statsBBATS.Empty() && statsBBATS[2] > 25;
				yield return timepause;

				bool charging = batteries.All(x => x.ChargeMode == ChargeMode.Recharge);
				rechargecancelled = (statsPW.Empty() && donescan) || (!statsPW.Empty() && statsPW[1] == 0) || lowbackupbat;
				bool notcharged = parked && parkedwithcn && donescan && ((!charging && !rechargecancelled) || (charging && rechargecancelled)) && !batteries.Empty();
				bool reassign = rechargecancelled && !statsPW.Empty() && statsPW[1] != 0 && comebackupbat;
				yield return timepause;

				/*if (!statsBBATS.Empty()) log.AppendNR($"--BB:{statsBBATS[2]}--\n");
				if (!statsPW.Empty()) log.AppendNR($"--PW:{statsPW[1]}--\n");
				log.AppendNR($"PW2:{statsPW.Empty() && BlockManager.Doneloop}");
				log.AppendNR($"NC:{notcharged}");
				log.AppendNR($"RA:{reassign}");*/

				if (!(!parked || notcharged || reassign)) {
					if (!parked) alreadyparked = false;
					EndBM(donescan);

					yield return timepause;
					continue; 
				}

				foreach (IMyGasTank t in tankblocks) {
					t.Stockpile = !rechargecancelled && parked && t.FilledRatio != 1;
					yield return timepause;
				}

				if (parked && !rechargecancelled) { //If I don't do this the ship will shut off
					foreach (IMyBatteryBlock b in backupbatteries)
					{
						b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Auto : ChargeMode.Recharge;
						yield return timepause;
					}
					foreach (IMyBatteryBlock b in batteries)
					{
						b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Recharge : ChargeMode.Auto;
						yield return timepause;
					}
				} else if (!parked || rechargecancelled) {
					foreach (IMyBatteryBlock b in batteries)
					{
						b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Recharge : ChargeMode.Auto;
						yield return timepause;
					}
					foreach (IMyBatteryBlock b in backupbatteries)
					{
						b.ChargeMode = parked && !rechargecancelled ? ChargeMode.Auto : ChargeMode.Recharge;
						yield return timepause;
					}
				}

				//batteries.ForEach(x => (x as IMyBatteryBlock).ChargeMode = parked ? ChargeMode.Recharge : ChargeMode.Auto);
				//backupbatteries.ForEach(x => x.ChargeMode = parked ? ChargeMode.Auto : ChargeMode.Recharge);

				if (!parked) alreadyparked = false;
				//Runtime.UpdateFrequency = update_frequency;

				//if (donescan) 
				EndBM(donescan);
				yield return timepause;
			}
		}


		void EndBM(bool scanned) {
			if (scanned && parked)
			{
				BlockManager.Doneloop = true;
				if (parked)
				{
					if (PerformanceWhilePark && gravLength == 0 && Runtime.UpdateFrequency != UpdateFrequency.Update100) Runtime.UpdateFrequency = UpdateFrequency.Update100;
					else if ((!PerformanceWhilePark || gravLength > 0) && Runtime.UpdateFrequency != UpdateFrequency.Update10) Runtime.UpdateFrequency = UpdateFrequency.Update10;
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
