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

        readonly List<IMyTextPanel> input_screens = new List<IMyTextPanel>();
		public IEnumerable<double> GetScreensSeq()
		{
			while (true) {
				log.AppendNR($"  Getting Screens => new:{input_screens.Count}");
				//bool greedy = this.greedy || this.applyTags || this.removeTags; //deprecated
				if (input_screens.Any()) this.screens.AddRange(input_screens);
				input_screens.Clear();

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
				log.AppendNR($"  ->Done. Total Screens {screens.Count} => Total Surfaces:{surfaces.Count}\n");
				GetScreen.Doneloop = true;
				yield return timepause;
			}
		}

		void GenerateProgressBar() {
			double percent = gearaccel / maxaccel;
			progressbar.Clear();
			progressbar.ProgressBar(percent, 30);
			trueaccel = $" ({(percent * totaleffectivethrust).Round(2)} {{m/s^2}}) ";
		}


		List<IMyTerminalBlock> batsseq = new List<IMyTerminalBlock>();
		List<double> outputbatsseq = new List<double>();

		public IEnumerable<double> GetBatStatsSeq()
		{
			while (true) {
				outputbatsseq.Clear();
				if (batsseq.Count > 0) {
					double inputs = 0;
					double outputs = 0;
					double percents = 0;
					foreach (IMyBatteryBlock b in batsseq)
					{
						inputs += b.CurrentInput;
						outputs += b.CurrentOutput;
						percents += b.CurrentStoredPower / b.MaxStoredPower;
						yield return timepause;
					}
					inputs /= backupbats.Count;
					outputs /= backupbats.Count;
					percents *= 100 / backupbats.Count;

					outputbatsseq = new List<double> { inputs, outputs, percents.Round(0) };
					yield return timepause;
				}
				BS.Doneloop = true;
				yield return timepause;
			}
		}

        readonly List<ShipController> controllers_input = new List<ShipController>();
		
		IEnumerable<double> GetControllersSeq()
		{
			while (true) {
				bool greedy = this.greedy || this.applyTags;// || this.removeTags;

				if (this.controllers_input.Count > 0) this.controllers.AddRange(controllers_input);
				controllers_input.Clear();

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

				if (controllers.Count == 0)
				{
					reason.AppendLine("No Controller Found.\nEither for missing tag, not working or removed.");
				}

				if (controlledControllers.Count == 0 /*&& usableControllers.Count == 0*/)
				{
					log.AppendNR("ERROR: no usable ship controller found. Reason: \n");
					log.AppendNR(reason.ToString(), false);
					ManageTag(true);
					shutdown = true;
					//Echo(log.ToString());
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

		readonly List<IMyMotorStator> abandonedrotors = new List<IMyMotorStator>();

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
					else*/ if (this.applyTags)
					{
						AddTag(r);
					}
				}

				foreach (IMyTerminalBlock tr in vtthrusters)
				{
					/*if (this.removeTags)
					{
						RemoveTag(tr);
					}
					else*/ if (this.applyTags)
					{
						AddTag(tr);
					}
				}

				rotors_input.AddRange(abandonedrotors);
				foreach (IMyMotorStator current in rotors_input)
				{
					/*if (this.removeTags)
					{
						RemoveTag(current);
					}
					else */if (this.applyTags)
					{
						AddTag(current);
					}

					bool cond = GridTerminalSystem.CanAccess(current) && !current.Closed && current.IsWorking && current.IsAlive();

					if (abandonedrotors.Contains(current)) abandonedrotors.Remove(current);

					if (cond && current.Top != null && (greedy || HasTag(current)) && current.TopGrid != Me.CubeGrid) { 
						Rotor rotor = new Rotor(current, this);
						this.vectorthrusters.Add(new VectorThrust(rotor, this));
						vtrotors.Add(current);
					}
					if (pauseseq) yield return timepause;
				}

				log.AppendNR("  >Getting Thrusters\n");
				// add all thrusters to their corrisponding nacelle and remove this.nacelles that have none
				for (int i = this.vectorthrusters.Count - 1; i >= 0; i--)
				{
					//if (!vtrotors.Contains(this.vectorthrusters[i].rotor.theBlock)) this will cause problems
					for (int j = thrusters_input.Count - 1; j >= 0; j--)
					{
						if (greedy || HasTag(thrusters_input[j])){ 

							/*if (this.removeTags)
							{
								RemoveTag(thrusters_input[j]);
							}*/

							bool cond = thrusters_input[j].CubeGrid == this.vectorthrusters[i].rotor.TheBlock.TopGrid;
							bool cond2 = this.vectorthrusters[i].thrusters.Any(x => vtthrusters.Any(y => y == x));

							// thruster is not for the current nacelle
							// if(!thrusters[j].IsFunctional) continue;// broken, don't add it

							if (cond && this.applyTags)
							{
								AddTag(thrusters_input[j]);
							}
							//if (this.vectorthrusters[i].thrusters.Any(x => vtthrusters.Contains(x.theBlock))) continue;
							//doesn't add it if it already exists
							if (cond && !cond2) {
								this.vectorthrusters[i].thrusters.Add(new Thruster(thrusters_input[j]));
								vtthrusters.Add(thrusters_input[j]);
								thrusters_input.RemoveAt(j);// shorten the list we have to check (It discards thrusters for next nacelle)
							}

						}
						if (pauseseq) yield return timepause;
					}
					// remove this.nacelles (rotors) without thrusters
					if (this.vectorthrusters[i].thrusters.Count == 0)
					{
						IMyMotorStator rotor = this.vectorthrusters[i].rotor.TheBlock;
						rotor.Brake();
						abandonedrotors.Add(rotor);
						RemoveTag(rotor);
						this.vectorthrusters.RemoveAt(i);// there is no more reference to the rotor, should be garbage collected (NOT ANYMORE, Added to abandoned rotors)
					}
					else {
						// if its still there, setup the nacelle
						this.vectorthrusters[i].ValidateThrusters(/*jetpack*/);
						this.vectorthrusters[i].DetectThrustDirection();
					}
					if (pauseseq) yield return timepause;
				}

				log.AppendNR("  >Grouping VTThrs\n");
				GroupVectorThrusters();

				if (VTThrGroups.Count == 0)
				{
					log.AppendNR("  > [ERROR] => Any Vector Thrusters Found!\n");
					shutdown = true;
					ManageTag(true);
					if (pauseseq) yield return timepause;
				}
				

				thrusters_input.Clear();
				rotors_input.Clear();
				GetVectorThrusters.Doneloop = true;
				yield return timepause;
			}
		}

		readonly List<IMyThrust> thrusters_input = new List<IMyThrust>();
		readonly List<IMyMotorStator> rotors_input = new List<IMyMotorStator>();

		readonly List<IMyThrust> vtthrusters = new List<IMyThrust>();
		readonly List<IMyMotorStator> vtrotors = new List<IMyMotorStator>();

		bool pauseseq = false;

		bool check = true;

		void CheckWeight() {
			ShipController cont = FindACockpit();
			if (check) {
				myshipmass = cont.TheBlock.CalculateShipMass();

				if (this.oldMass != myshipmass.BaseMass) {
					log.AppendNR("New weight encountered, checking again\n");
					ResetVTHandlers();
				}
				if (!justCompiled) GenerateProgressBar();
				this.oldMass = myshipmass.BaseMass;
				return; 
			}
			
			if (cont == null)
			{
				log.AppendNR("  -No cockpit registered, checking everything\n");
			}
			else if (!applyTags)
			{
				myshipmass = cont.TheBlock.CalculateShipMass();
				if (myshipmass.BaseMass < 0.001f)
				{
					string msg = "  -Can't fly a Station";
					log.AppendNR(msg);
					isstation = true;
					Runtime.UpdateFrequency = UpdateFrequency.Update100;
					return;
				}
				else if (isstation)
				{
					isstation = false;
					Runtime.UpdateFrequency = update_frequency;
				}
				if (this.oldMass == myshipmass.BaseMass)
				{
					log.AppendNR("  -Mass is the same, everything is good\n");
					check = false;
					return;
				}
				
			}
			log.AppendNR("  -Mass is different, checking everything\n");
			this.oldMass = myshipmass.BaseMass;
			OneRunMainChecker(false);
			if (!justCompiled) GenerateProgressBar();
		}

		

		public IEnumerable<double> CheckVectorThrustersSeq()
		{
			while (true) {
				pauseseq = ((!justCompiled || (justCompiled && shutdown)) && !applyTags);
				if (pauseseq) yield return timepause;

				if (!check) {
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
					continue;
				}

				List<IMyTerminalBlock> allblocks = new List<IMyTerminalBlock>(parkblocks);
				allblocks = allblocks
					.Concat(tankblocks)
					.Concat(batteriesblocks)
					.Concat(gridbats)
					.Concat(cruiseThr)
					.Concat(normalThrusters)
					.Concat(controllerblocks)
					.Concat(ccontrollerblocks)
					.Concat(vtrotors)
					.Concat(abandonedrotors) // to remove deleted ones, don't panic if you don't find this variable anywhere
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
							parkblocks.Remove(b);
						}
						else if (b is IMyGasTank)
						{
							tankblocks.Remove(b);
						}
						else if (b is IMyBatteryBlock)
						{
							batteriesblocks.Remove(b);
							if (FilterThis(b)) gridbats.Remove(b);
						}
						else if (b is IMyMotorStator)
						{
							vtrotors.Remove((IMyMotorStator)b);
						}
						else if (b is IMyThrust)
						{
							if (vtthrusters.Contains(b)) vtthrusters.Remove((IMyThrust)b);
							else if (FilterThis(b))
							{
								if (cruiseThr.Contains(b)) cruiseThr.Remove((IMyThrust)b);
								normalThrusters.Remove((IMyThrust)b);
							}
						}
						else if (b is IMyShipController) {
							if (ccontrollerblocks.Contains(b)) ccontrollerblocks.Remove((IMyShipController)b);
							controllerblocks.Remove((IMyShipController)b);
						}
						else if (b is IMyTextPanel)
						{
							screens.Remove((IMyTextPanel)b);
							RemoveSurface((IMyTextPanel)b);
						}
						
					}
					else if (applyTags && (tagallcond || tagcond)) {
						log.AppendNR("Adding tag:" + b.CustomName);
						AddTag(b);						
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

				foreach (VectorThrust vt in vectorthrusters) {
					vt.thrusters.RemoveAll(x => !vtthrusters.Contains(x.TheBlock));
					vt.activeThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
					vt.availableThrusters.RemoveAll(x => !vt.thrusters.Contains(x));
				}

				if (pauseseq) yield return timepause;
				foreach (List<VectorThrust> group in VTThrGroups) {
					group.RemoveAll(x => !vectorthrusters.Contains(x) || x.thrusters.Count < 1);
				}
				VTThrGroups.RemoveAll(x => x.Count < 1);

				for (int i = controlledControllers.Count - 1; i >= 0; i--)
				{
					if (!GridTerminalSystem.CanAccess(controlledControllers[i].TheBlock))
					{
						RemoveSurfaceProvider(controlledControllers[i].TheBlock);
						controlledControllers.RemoveAt(i);
					}
					if (pauseseq) yield return timepause;
				}

				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
				GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);

				blocks = blocks.Except(parkblocks)
					.Except(tankblocks)
					.Except(batteriesblocks)
					.Except(gridbats)
					.Except(cruiseThr)
					.Except(normalThrusters)
					.Except(ccontrollerblocks)
					.Except(controllerblocks)
					.Except(vtrotors)
					.Except(vtthrusters)
					.Except(screens)
					.ToList();

				if (pauseseq) yield return timepause;

				//artificial scope (Removed)
				foreach (IMyTerminalBlock b in blocks)
				{

					bool island = b is IMyLandingGear;
					if (b is IMyShipController)
					{
						controllerblocks.Add((IMyShipController)b);
						controllers_input.Add(new ShipController((IMyShipController)b));
					}
					else if (b is IMyMotorStator)
					{
						IMyMotorStator rt = (IMyMotorStator)b;
						if (justCompiled)
						{
							rt.TargetVelocityRPM = 0;
							rt.RotorLock = false;
							rt.Enabled = true;
						}
						rotors_input.Add(rt);

					}
					else if(b is IMyThrust)
					{
						IMyThrust tr = (IMyThrust)b;

						if (justCompiled)
						{
							tr.ThrustOverridePercentage = 0;
							tr.Enabled = true;
						}

						thrusters_input.Add(tr);

						if (FilterThis(b))
						{
							normalThrusters.Add((IMyThrust)b);

							if (b.Orientation.Forward == mainController.TheBlock.Orientation.Forward) //changing
								cruiseThr.Add((IMyThrust)b);
						}
					}
					/*if (b is IMyProgrammableBlock) // I will use it in the future maybe for autopilot
					{
						programBlocks.Add((IMyProgrammableBlock)b);
					}*/
					else if(b is IMyTextPanel)
					{
						input_screens.Add((IMyTextPanel)b);
					}
					
					else if((b is IMyShipConnector || island) && ((ConnectorNeedsSuffixToPark && HasTag(b)) || TagAll || (((!ConnectorNeedsSuffixToPark && !HasTag(b)) || (island && FilterThis(b))) && FilterThis(b))))
					{
						if (TagAll) AddTag(b);
						parkblocks.Add(b);
					}
					else if(b is IMyGasTank && (HasTag(b) || TagAll || FilterThis(b)))
					{
						if (TagAll) AddTag(b);
						tankblocks.Add(b);
					}
					else if(b is IMyBatteryBlock)
					{
						if (TagAll) AddTag(b);
						if (justCompiled) (b as IMyBatteryBlock).ChargeMode = ChargeMode.Auto;
						if (HasTag(b)) batteriesblocks.Add(b);
						else if (FilterThis(b)) gridbats.Add(b);
					}
					if (pauseseq) yield return timepause;
				}

				// TODO: Compare if blocks are equal, or make other quick way to gather correct blocks (DONE)

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

				check = false;
				yield return timepause;
			}
		}


		public IEnumerable<double> BlockManager() {
			while (true) {
				bool cond_thr = (parked && turnedoffthusters.Count == 0) || (!parked && turnedoffthusters.Count != 0);
				bool cond_rec = (!parked && rechargedblocks.Count > 0) || (parked && rechargedblocks.Count == 0);

				if (cond_thr)
				{
					if (TurnOffThrustersOnPark && normalThrusters.Count > 0)
						foreach (IMyFunctionalBlock tr in normalThrusters)
						{
							bool p = (parked && tr.Enabled);
							if ((!parked && !tr.Enabled) || (parked && tr.Enabled))
							{
								tr.Enabled = !parked;
								if (p) turnedoffthusters.Add(tr);
							}
							yield return timepause;
						}
					if (!parked) turnedoffthusters.Clear();
				}

				yield return timepause;

				List<double> stats;
				bool bba = backupbats.Count > 0;

				batsseq = backupbats;
				while (batsseq.Count > 0 && !BS.Doneloop)
				{
					BS.Run();
					yield return timepause;
				}
				BS.Doneloop = false;
				stats = outputbatsseq;
				yield return timepause;

				bool reassign = false;
				if (parked && !rechargecancelled && stats.Count > 0)
				{

					if (stats[0] < stats[1])
					{
						dischargingtimer += Runtime.TimeSinceLastRun.TotalSeconds;
					}
					else
					{
						dischargingtimer = 0;
					}

					if (dischargingtimer >= 5 || stats[2] < 0.1)
					{
						List<IMyTerminalBlock> bats = gridbats;
						bats.AddRange(batteriesblocks);
						foreach (IMyBatteryBlock b in bats)
						{
							b.ChargeMode = ChargeMode.Auto;
							yield return timepause;
						}

						rechargecancelled = true;
						rechargedblocks.Clear();
						backupbats.Clear();
					}
				}
				else if (parked && rechargecancelled) {
					if (stats[0] >= stats[1] && stats[2] >= 90) {
						reassign = true;
						rechargecancelled = false;
					}
				}
				else if (!parked)
				{
					rechargecancelled = false;
					dischargingtimer = 0;
				}

				yield return timepause;

				int gbc = gridbats.Count;
				int bbc = batteriesblocks.Count;
				if (((tankblocks.Count > 0 || gbc > 0 || bbc > 0) && ((parked && cnparks > 0) || !parked) && cond_rec && !rechargecancelled) || reassign)
				{
					

					if (!reassign || !rechargecancelled) {
						foreach (IMyFunctionalBlock t in tankblocks)
						{
							if (t is IMyGasTank) (t as IMyGasTank).Stockpile = parked;
							rechargedblocks.Add(t);
							yield return timepause;
						}
					}

					List<IMyTerminalBlock> bats = gridbats;
					bats.AddRange(batteriesblocks);
					int bl = bats.Count;

					if (bl > 0)
					{
						if (parked || reassign)
						{
							for (int i = 1; i < bats.Count; i++) {

								(bats[i] as IMyBatteryBlock).ChargeMode = ChargeMode.Recharge;
								rechargedblocks.Add(bats[i]);
								yield return timepause;
							}

							backupbats.Add(bats[0]);

							batsseq = backupbats;
							while (!BS.Doneloop)
							{
								BS.Run();
								yield return timepause;
							}
							stats = outputbatsseq;
							BS.Doneloop = false;

							if (stats[2] < 0.1)
							{
								foreach (IMyBatteryBlock b in bats)
								{
									b.ChargeMode = ChargeMode.Auto;
									yield return timepause;
								}
								rechargecancelled = true;
								rechargedblocks.Clear();
								backupbats.Clear();
							}
						}
						else
						{ //I prefer to loop all instead
							foreach (IMyBatteryBlock b in bats)
							{
								b.ChargeMode = ChargeMode.Auto;
								yield return timepause;
							}
							rechargedblocks.Clear();
							backupbats.Clear();
						}

					}
				}
				if (!parked) BM.Doneloop = false;
				else BM.Doneloop = true;
				yield return timepause;
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
