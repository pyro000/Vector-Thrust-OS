using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string dampenersArg = "dampeners";
        const string cruiseArg = "cruise";
        const string gearArg = "gear";
        const string applyTagsArg = "applytags";
        const string applyTagsAllArg = "applytagsall";
        const string removeTagsArg = "removetags";
        string oldTag = "";
        string tag = "|VT|";
        string textSurfaceKeyword = "VT:";
        string LCDName = "VTLCD";

        readonly SequenceAssigner BlockManager;
        readonly SequenceAssigner BatteryStats;

        //readonly double timeperframe = 1.0 / 60.0;
        const double TOVval = 0.25;
        double sv = 0;
        double wgv = 0;
        double mvin = 0;
        double accel = 0;
        double maxaccel = 0;
        double totaleffectivethrust = 0;
        double totalVTThrprecision = 0;
        double force = 0;
        double displaygearaccel = 0;
        double tgotTOV = 0;
        double rawgearaccel = 0;
        //double tthrust = 0;
        double len = 0;
        double global_thrustbynthr = 0;

        Vector3D desiredVec = Vector3D.Zero; //new Vector3D();
        Vector3D worldGrav = Vector3D.Zero;
        Vector3D shipVelocity = Vector3D.Zero;
        Vector3D lastvelocity = Vector3D.Zero;
        Vector3D global_requiredVec = Vector3D.Zero;

        bool dampchanged = false;
        bool parked = false;
        bool alreadyparked = false;
        bool cruisedNT = false;
        bool setTOV = false;
        bool TagAll = false;
        bool error = false;
        bool oldDampeners = false;
        bool isstation = false;
        bool trulyparked = false;
        bool parkavailable = false;
        bool almostbraked = false;
        bool cruise = false;
        bool dampeners = true;
        bool dampenersIsPressed = false;
        bool cruiseIsPressed = false;
        bool gearIsPressed = false;
        bool allowparkIsPressed = false;
        bool rotorsstopped = false;
        bool justCompiled = true;
        bool pauseseq = false;
        bool check = true;
        bool applyTags = false;
        bool greedy = true;
        bool thrustOn = true;
        bool rechargecancelled = false;
        bool parkedwithcn = false;
        bool unparkedcompletely = true;
        bool parkedcompletely = false;
        bool changedruntime = false;
        bool forceunpark = false;
        bool cruisebyarg = false;
        bool changeDampeners = false;
        bool global_gravChanged = false;

        readonly StringBuilder echosb = new StringBuilder();
        readonly StringBuilder screensb = new StringBuilder();
        readonly StringBuilder log = new StringBuilder();
        readonly StringBuilder surfaceProviderErrorStr = new StringBuilder();

        int SkipFrame = 0;
        readonly int updatespersecond = 60;
        int blockcount = 0;

        readonly SequenceAssigner MainChecker;
        readonly SequenceAssigner GetScreen;
        readonly SequenceAssigner GetControllers;
        readonly SequenceAssigner GetVectorThrusters;
        readonly SequenceAssigner CheckParkBlocks;
        readonly SequenceAssigner Assign;

        readonly WhipsHorizon WH;
        readonly Tracker tracker;

        const float defaultAccel = 1f;
        const float accelBase = 1.5f;//accel = defaultAccel * g * base^exponent
                                     // your +, - and 0 keys increment, decrement and reset the exponent respectively
                                     // this means increasing the base will increase the amount your + and - change target acceleration
        const float dampenersModifier = 0.1f; // multiplier for dampeners, higher is stronger dampeners		 
        const float zeroGAcceleration = 9.81f; // default acceleration in situations with 0 (or low) gravity				 
        const float gravCutoff = 0.1f * zeroGAcceleration;  // if gravity becomes less than this, zeroGAcceleration will kick in (I think it's deprecated)
        float gravLength = 0;
        float lastGrav = 0;
        float oldMass = 0;
        float accel_aux = 0;

        // Control Module params... this can always be true, but it's deprecated
        bool controlModule = true;
        const string dampenersButton = "c.damping"; //Z
        const string cruiseButton = "c.cubesizemode"; //R
        const string gearButton = "c.sprint"; //Shift
        const string allowparkButton = "c.thrusts";//X 
        //"c.stationrotation"; //B
        //double totalaccel = 0;

        ShipController mainController = null;
        MyShipMass myshipmass;

        List<ShipController> controllers = new List<ShipController>();
        List<IMyShipController> controllerblocks = new List<IMyShipController>();
        List<IMyShipController> ccontrollerblocks = new List<IMyShipController>();
        List<ShipController> controlledControllers = new List<ShipController>();
        List<VectorThrust> vectorthrusters = new List<VectorThrust>();
        List<IMyThrust> normalThrusters = new List<IMyThrust>();
        List<IMyTextPanel> screens = new List<IMyTextPanel>();
        readonly List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        readonly List<IMyLandingGear> landinggears = new List<IMyLandingGear>();
        List<IMyGasTank> tankblocks = new List<IMyGasTank>();
        readonly List<IMyTerminalBlock> cruiseThr = new List<IMyTerminalBlock>();
        readonly List<List<VectorThrust>> VTThrGroups = new List<List<VectorThrust>>();
        List<Surface> surfaces = new List<Surface>();
        List<IMyThrust> vtthrusters = new List<IMyThrust>();
        List<IMyMotorStator> vtrotors = new List<IMyMotorStator>();
        List<IMyBatteryBlock> taggedbats = new List<IMyBatteryBlock>();
        List<IMyBatteryBlock> normalbats = new List<IMyBatteryBlock>();
        List<IMyThrust> thrusters_input = new List<IMyThrust>();
        List<IMyMotorStator> rotors_input = new List<IMyMotorStator>();
        readonly List<ShipController> controllers_input = new List<ShipController>();
        List<IMyTextPanel> input_screens = new List<IMyTextPanel>();
        readonly List<IMyMotorStator> abandonedrotors = new List<IMyMotorStator>();
        readonly List<IMyThrust> abandonedthrusters = new List<IMyThrust>();
        List<IMySoundBlock> soundblocks = new List<IMySoundBlock>();
        List<IMyShipConnector> connectorblocks = new List<IMyShipConnector>();
        List<IMyLandingGear> landinggearblocks = new List<IMyLandingGear>();
        List<IMyBatteryBlock> batteriesblocks = new List<IMyBatteryBlock>();
        List<IMyTerminalBlock> batsseq = new List<IMyTerminalBlock>();
        readonly List<IMyTerminalBlock> AllBlocks = new List<IMyTerminalBlock>();
        List<double> outputbatsseq = new List<double>();
        List<double> tets = new List<double>();
        List<int> tdividers = new List<int> { 1, 1 };

        Dictionary<string, object> CMinputs = null;

        public Program()
        {
            log.AppendLine("Program() Start");

            Load();
            //_RuntimeTracker = new RuntimeTracker(this, 60, 0.005);

            tracker = new Tracker(this, 100, 500);

            BlockManager = new SequenceAssigner(this, BlockManagerSeq(), true);
            BatteryStats = new SequenceAssigner(this, GetBatStatsSeq(), true);
            MainChecker = new SequenceAssigner(this, CheckVectorThrustersSeq(), true);
            GetScreen = new SequenceAssigner(this, GetScreensSeq(), true);
            GetControllers = new SequenceAssigner(this, GetControllersSeq(), true);
            GetVectorThrusters = new SequenceAssigner(this, GetVectorThrustersSeq(), true);
            CheckParkBlocks = new SequenceAssigner(this, CheckParkBlocksSeq(), true);

            Assign = new SequenceAssigner(this, AssignSeq(), true);

            WH = new WhipsHorizon(surfaces, this);
            Init();
            
            if (!error) Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Echo(log.ToString());
            log.AppendLine("--VTOS Started--");
        }

        public void Save()
        {
            string save = string.Join(";", string.Join(":", tag, greedy), allowpark, gear, cruise);
            Storage = save; //saving the old tag and greedy to prevent recompile or script update confusion
        }

        public void Load() {
            string[] saved = Storage.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            if (saved.Length > 0)
            {
                string[] stg = saved[0].Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (stg.Length == 2)
                {
                    oldTag = stg[0]; //loading tag
                    greedy = bool.Parse(stg[1]); //loading greedy
                }

                if (saved.Length >= 2) allowpark = bool.Parse(saved[1]); //Gets if the user set in park mode when recompile or reload, prevents accidents

                if (saved.Length >= 3)
                {
                    int g = int.Parse(saved[2]);
                    if (Accelerations.Count - 1 < g) g = Accelerations.Count - 1; //changed
                    gear = g;
                }

                if (saved.Length >= 4) {
                    cruise = bool.Parse(saved[3]); ;
                }
            }
        }

        public void Main(string argument)
        {
            // ========== STARTUP ==========
            argument = argument.ToLower();

            tracker.Process();
            Config();

            // GETTING ONLY NECESARY INFORMATION TO THE SCRIPT
            if (!parkedcompletely || argument.Length > 0 || trulyparked)
            {
                MyShipVelocities shipVelocities = mainController.TheBlock.GetShipVelocities();
                shipVelocity = shipVelocities.LinearVelocity;
                sv = shipVelocity.Length();

                if (!parkedcompletely || argument.Length > 0) { 
                    bool damp = mainController.TheBlock.DampenersOverride;
                    dampchanged = damp != oldDampeners;
                    oldDampeners = damp;

                    desiredVec = GetMovementInput(argument, parked);
                    mvin = desiredVec.Length();

                    almostbraked = mvin == 0 && sv < velprecisionmode;
                    GetAcceleration();
                    ThrustOnHandler();
                }
            }

            Printer(argument.Length > 0); //PRINTER MUST BE HERE BECUASE OF GetMovIn

            // END NECESARY INFORMATION
            //_RuntimeTracker.RegisterAction("Action");
            //echosb.AppendLine($"{tracker.JustPrinted} {tracker.LastRuntime.Truncate(2)}");   

            // SKIPFRAME AND PERFORMANCE HANDLER: handler to skip frames, it passes out if the player doesn't parse any command or do something relevant.
            CheckWeight(); //Block Updater must-have
            if (SkipFrameHandler(argument)) return;
            // END SKIPFRAME

            // ========== PHYSICS ==========
            //TODO: SEE IF I CAN SPLIT AT LEAST SOME OF THE STEPS BY SEQUENCES
            float shipMass = myshipmass.PhysicalMass;
            worldGrav = mainController.TheBlock.GetNaturalGravity();
            gravLength = (float)worldGrav.Length();

            bool gravChanged = Math.Abs(lastGrav - gravLength) > 0.05f;
            wgv = lastGrav = gravLength;

            // setup gravity
            if (gravLength < gravCutoff) gravLength = zeroGAcceleration;

            // f=ma
            Vector3D shipWeight = shipMass * worldGrav;

            if (dampeners)
            {
                Vector3D dampVec = Vector3D.Zero;
                if (desiredVec != Vector3D.Zero)
                {
                    // cancel movement opposite to desired movement direction
                    if (Extensions.Dot(desiredVec, shipVelocity) < 0)
                    {
                        //if you want to go oppisite to velocity
                        dampVec += VectorMath.Projection(shipVelocity, desiredVec.Normalized());
                    }
                    // cancel sideways movement
                    dampVec += VectorMath.Rejection(shipVelocity, desiredVec.Normalized());
                }
                else
                {
                    dampVec += shipVelocity;
                }


                if (cruise)
                {
                    if (cruiseThr.Count > 0 && !cruisedNT)
                    {
                        cruisedNT = true;
                        foreach (IMyFunctionalBlock b in cruiseThr) b.Enabled = false;
                    }

                    foreach (ShipController cont in controlledControllers)
                    {
                        if ((OnlyMain() && cont != mainController) || !cont.TheBlock.IsUnderControl) continue;

                        if (Extensions.Dot(dampVec, cont.TheBlock.WorldMatrix.Forward) > 0 || cruisePlane)
                        { // only front, or front+back if cruisePlane is activated
                            dampVec -= VectorMath.Projection(dampVec, cont.TheBlock.WorldMatrix.Forward);
                        }

                        if (cruisePlane)
                        {
                            shipWeight -= VectorMath.Projection(shipWeight, cont.TheBlock.WorldMatrix.Forward);//shipWeight.Project(cont.TheBlock.WorldMatrix.Forward);
                        }
                    }
                }
                else if (!cruise && cruisedNT)
                {
                    cruisedNT = false;
                    cruiseThr.ForEach(b => (b as IMyFunctionalBlock).Enabled = true);
                }

                cruise = justCompiled || (wgv != 0 && sv == 0) || trulyparked || cruiseThr.Empty() || cruisebyarg || parked || alreadyparked || BlockManager.Doneloop ? cruise : cruiseThr.All(x => !(x as IMyFunctionalBlock).Enabled); //New cruise toggle mode

                desiredVec -= dampVec * dampenersModifier;
            }
            // f=ma
            lastvelocity = shipVelocity;
            desiredVec *= shipMass * (float)accel;

            // point thrust in opposite direction, add weight. this is force, not acceleration
            Vector3D requiredVec = -desiredVec + shipWeight;

            // remove thrust done by normal thrusters
            Vector3D nthrthrust = Vector3D.Zero;
            
            foreach (IMyThrust t in normalThrusters)
            {
                nthrthrust += t.WorldMatrix.Backward * t.CurrentThrust;
            }
            double thrustbynthr = nthrthrust.Length();
            requiredVec += nthrthrust;

            len = requiredVec.Length();
            // ========== END OF PHYSICS ==========


            // ========== DISTRIBUTE THE FORCE EVENLY BETWEEN NACELLES ==========
            ParkVector(ref requiredVec, shipMass);

            global_requiredVec = requiredVec;
            global_gravChanged = gravChanged;
            global_thrustbynthr = thrustbynthr;

            Assign.Run();
            justCompiled = false;
            // ========== END OF MAIN ==========
        }

        IEnumerable<int> AssignSeq()
        {
            while (true)
            {

                Vector3D requiredVec = global_requiredVec;

                double totalVTThrprecision_aux = 0;
                double totaleffectivethrust_aux = 0;
                //double tthrust_aux = 0;
                double rawgearaccel_aux = 0;

                // NEW THRUSTER ALIGNMENT AND VECTOR ASSIGNMENT SYSTEM
                int gc = VTThrGroups.Count;

                List<List<VectorThrust>> VTGroups = new List<List<VectorThrust>>(VTThrGroups);

                for (int i = 0; i < gc; i++)
                {
                    List<VectorThrust> g = VTGroups[i];

                    int tc = g.Count;
                    if (tc <= 0) continue;

                    int ni = i + 1;

                    //Print($"Group {ni}/{gc}");

                    int c = g.Count(x => x.totalEffectiveThrust > 0).Clamp(1, tc);
                    // This for some reason fixes a crash on station grids on compile, also from parking

                    Vector3D vectemp = VectorMath.Rejection(requiredVec, g[0].rotor.TheBlock.WorldMatrix.Up);

                    if (g[0].rotor.IsHinge && Vector3D.Dot(vectemp, g[0].rotor.TheBlock.WorldMatrix.Left) <= 0)
                    { //is pointed left
                        vectemp = VectorMath.Rejection(vectemp, g[0].rotor.TheBlock.WorldMatrix.Right);
                    }

                    if (ni < gc && tets[ni] > 0 && tets[i] >= vectemp.Length())
                    {
                        VectorThrust nextvt = VTThrGroups[ni][0];
                        Vector3D nextvectemp = VectorMath.Rejection(vectemp, nextvt.rotor.TheBlock.WorldMatrix.Up);

                        if (nextvt.rotor.IsHinge)
                        {
                            bool nisPointedLeft = Vector3D.Dot(nextvectemp, nextvt.rotor.TheBlock.WorldMatrix.Left) > 0;
                            if (!nisPointedLeft) nextvectemp = VectorMath.Rejection(nextvectemp, nextvt.rotor.TheBlock.WorldMatrix.Right);
                        }

                        nextvectemp *= 0.15; //Gifting 15% of the vector to the next group
                        nextvectemp = nextvectemp.Clamp(0.01, tets[ni]); //limiting the vector's with the previous totaleffectivethrust

                        vectemp -= nextvectemp;
                    }

                    tets[i] = 0;
                    vectemp /= c;

                    int div = (g.Count / tdividers[0]).Clamp(1, g.Count);

                    List<VectorThrust> Vts = new List<VectorThrust>(g);

                    for (int j = 0; j < tc; j++)
                    {
                        VectorThrust vt = Vts[j];

                        bool enabledop = div != tc;

                        if (!CheckRotor(vt.rotor.TheBlock)) continue;

                        if (!vt.thrusters.Empty() && (global_gravChanged || !vt.ValidateThrusters()))
                        {
                            vt.DetectThrustDirection();
                        }

                        double tet = vt.CalcTotalEffectiveThrust();

                        tets[i] += tet;

                        if (tet <= 0) tet = 0.01;
                        else rawgearaccel_aux += tet;

                        vt.requiredVec = vectemp.Clamp(0.01, tet);

                        vt.Go();

                        requiredVec -= vt.requiredVec;

                        totaleffectivethrust_aux += tet * 1.595;
                        totalVTThrprecision_aux += vt.rotor.LastAngleCos;

                        if (enabledop) {
                            //Print("Enabled VT");
                            if ( j + 1 % div == 0)
                            {
                                //echosb.AppendLine($"Dividing 1 {j}/{div}");
                                yield return 1;
                            }
                        }
                    }
                }

                if (rawgearaccel_aux != 0)
                {
                    rawgearaccel = rawgearaccel_aux;
                    rawgearaccel += global_thrustbynthr;
                    rawgearaccel /= myshipmass.PhysicalMass;
                }

                totalVTThrprecision_aux /= vectorthrusters.Count;

                //tthrust_aux += global_thrustbynthr;
                //tthrust_aux /= myshipmass.TotalMass;
                totaleffectivethrust_aux += global_thrustbynthr; //DON'T DELETE THIS, THIS SOLVES THE THRUST POINTING TO THE OPPOSITE

                totalVTThrprecision = totalVTThrprecision_aux;
                //tthrust = tthrust_aux;
                totaleffectivethrust = totaleffectivethrust_aux;

                yield return 1;
            }
        }
    }
}
