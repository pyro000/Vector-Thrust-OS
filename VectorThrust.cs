
// weather or not dampeners or thrusters are on when you start the script
public bool dampeners = true;
public bool jetpack = true;

public bool controlModule = true;


public const float defaultAccel = 1f;//this is the default target acceleration you see on the display
// if you want to change the default, change this
// note, values higher than 1 will mean your nacelles will face the ground when you want to go
// down rather than just lower thrust
// '1g' is acceleration caused by current gravity (not nessicarily 9.81m/s) although
// if current gravity is less than 0.1m/s it will ignore this setting and be 9.81m/s anyway

public const float accelBase = 1.5f;//accel = defaultAccel * g * base^exponent
// your +, - and 0 keys increment, decrement and reset the exponent respectively
// this means increasing the base will increase the amount your + and - change target cceleration

// arguments, you can change these to change what text you run the programmable block with
public const string standbyArg = "%standby";
public const string dampenersArg = "%dampeners";
public const string jetpackArg = "%jetpack";
public const string raiseAccelArg = "%raiseAccel";
public const string lowerAccelArg = "%lowerAccel";
public const string resetAccelArg = "%resetAccel";
public const string resetArg = "%reset";
public const string checkNacellesArg = "%checkNacelles";

// control module gamepad bindings
// type "/cm showinputs" into chat
// press the desired button
// put that text EXACTLY as it is in the quotes for the control you want
public const string jetpackButton = "c.thrusts";
public const string dampenersButton = "c.damping";
public const string lowerAccel = "minus";
public const string raiseAccel = "plus";
public const string resetAccel = "0";

public const float maxRotorRPM = 60;

public const bool verboseCheck = true;

/////////////////////////////////////////////
public const float zeroGAcceleration = 9.81f;// acceleration in situations with 0 (or low) gravity
public const float gravCutoff = 0.1f * 9.81f;// if gravity becomes less than this, zeroGAcceleration will kick in
/////////////////////////////////////////////

public Program() {
	Echo("Just Compiled");
    init();
    Echo("Ready to Run");
}
public void Save() {}

public List<Nacelle> nacelles;
public int rotorCount = 0;
public int thrusterCount = 0;
public IMyShipController controller;

public void Main(string argument) {
	Echo("Starting Main");
	if(argument.Equals(resetArg)) {
		init();
	} else {
		checkNacelles(verboseCheck);
	}
	if(argument.Equals(checkNacellesArg)) {
		Echo("Checking Nacelles");
		foreach(Nacelle n in nacelles) {
			n.detectThrustDirection();
			Echo($"{n.errStr}");
		}
	}

	/*TODO: look over this*/
	// get controller position
	MatrixD controllerMatrix = controller.WorldMatrix;

 	// get gravity in world space
	Vector3D worldGrav = controller.GetNaturalGravity();

	// get velocity
	MyShipVelocities shipVelocities = controller.GetShipVelocities();
	Vector3D shipVelocity = shipVelocities.LinearVelocity;
	// Vector3D shipAngularVelocity = shipVelocities.AngularVelocity;

	// setup mass
	MyShipMass myShipMass = controller.CalculateShipMass();
	float shipMass = myShipMass.PhysicalMass;

	// setup gravity
	float gravLength = (float)worldGrav.Length();
	if(gravLength < gravCutoff) {
		gravLength = zeroGAcceleration;
	}

	Vector3D desiredVec = getMovement(controllerMatrix, argument);

	// f=ma
	Vector3D shipWeight = shipMass * worldGrav;
	// f=ma
	desiredVec *= shipMass * (float)getAcceleration(gravLength);

	/*TODO: look over previous*/

	// point thrust in opposite direction, add weight. this is force, not acceleration
	Vector3D requiredVec = -desiredVec + shipWeight;
	Echo("Vectors done");

	// update thrusters on/off and re-check nacelles direction
	foreach(Nacelle n in nacelles) {
		if(!n.validateThrusters()) {
			n.detectThrustDirection();
		}
	}
	Echo("nacelle checks done");




	/* TOOD: redo this */
	// group similar nacelles (rotor axis is same direction)
	List<List<Nacelle>> nacelleGroups = new List<List<Nacelle>>();
	for(int i = 0; i < nacelles.Count; i++) {
		bool foundGroup = false;
		foreach(List<Nacelle> g in nacelleGroups) {// check each group to see if its lined up
			if(Math.Abs(Vector3D.Dot(nacelles[i].rotor.wsAxis, g[0].rotor.wsAxis)) > 0.9f) {
				g.Add(nacelles[i]);
				foundGroup = true;
				break;
			}
		}
		if(!foundGroup) {// if it never found a group, add a group
			nacelleGroups.Add(new List<Nacelle>());
			nacelleGroups[nacelleGroups.Count-1].Add(nacelles[i]);
		}
	}
	Echo("nacelle groups done");

	// correct for misaligned nacelles
	Vector3D asdf = Vector3D.Zero;
	// 1
	foreach(List<Nacelle> g in nacelleGroups) {
		g[0].requiredVec = Vector3D.Reject(requiredVec, g[0].rotor.wsAxis);
		asdf += g[0].requiredVec;
	}
	// 2
	asdf -= requiredVec;
	// 3
	foreach(List<Nacelle> g in nacelleGroups) {
		g[0].requiredVec -= asdf;
	}
	// 4
	asdf /= nacelleGroups.Count;
	// 5
	foreach(List<Nacelle> g in nacelleGroups) {
		g[0].requiredVec += asdf;
	}
	// apply first nacelle settings to rest in each group
	foreach(List<Nacelle> g in nacelleGroups) {
		Vector3D req = g[0].requiredVec / g.Count;
		for(int i = 0; i < g.Count; i++) {
			g[i].requiredVec = req;
			g[i].go();
			Echo(g[i].errStr);
			foreach(Thruster t in g[i].activeThrusters) {
				Echo($"Thruster: {t.theBlock.CustomName}\n{t.errStr}");
			}
		}
	}/* end of TODO */

	Echo("all done");

}


public int accelExponent = 0;

public bool jetpackIsPressed = false;
public bool dampenersIsPressed = false;
public bool plusIsPressed = false;
public bool minusIsPressed = false;


double getAcceleration(double gravity) {
	return Math.Pow(accelBase, accelExponent) * gravity * defaultAccel;
}

/**/
// TODO: look over this
public Vector3D getMovement(MatrixD controllerMatrix, string arg) {
	Vector3 moveVec = Vector3.Zero;

	if(controlModule) {
		// setup control module
		Dictionary<string, object> inputs = new Dictionary<string, object>();
		try {
			inputs = Me.GetValue<Dictionary<string, object>>("ControlModule.Inputs");
		} catch(Exception e) {
			controlModule = false;
		}

		// non-movement controls
		if(inputs.ContainsKey(dampenersButton) && !dampenersIsPressed) {//inertia dampener key
			dampeners = !dampeners;//toggle
			dampenersIsPressed = true;
			// this doesn't work when there are no thrusters on the same grid as the cockpit
			// dampeners = controller.GetValue<bool>("DampenersOverride");
		}
		if(!inputs.ContainsKey(dampenersButton)) {
			dampenersIsPressed = false;
		}
		if(inputs.ContainsKey(jetpackButton) && !jetpackIsPressed) {//jetpack key
			jetpack = !jetpack;//toggle
			jetpackIsPressed = true;
		}
		if(!inputs.ContainsKey(jetpackButton)) {
			jetpackIsPressed = false;
		}
		if(inputs.ContainsKey(raiseAccel) && !plusIsPressed) {//throttle up
			accelExponent++;
			plusIsPressed = true;
		}
		if(!inputs.ContainsKey(raiseAccel)) { //increase target acceleration
			plusIsPressed = false;
		}

		if(inputs.ContainsKey(lowerAccel) && !minusIsPressed) {//throttle down
			accelExponent--;
			minusIsPressed = true;
		}
		if(!inputs.ContainsKey(lowerAccel)) { //lower target acceleration
			minusIsPressed = false;
		}
		if(inputs.ContainsKey(resetAccel)) { //default throttle
			accelExponent = 0;
		}

		// movement controls
		try {
			moveVec = (Vector3)inputs["c.movement"];
		} catch(Exception e) {
			// no movement
		}
	} else {
		moveVec = controller.MoveIndicator;
		// Vector2 roll = controller.RotationIndecator;
		// float roll = controller.RollIndecator;
	}

	if(arg.Contains(dampenersArg)) {
		dampeners = !dampeners;
	}
	if(arg.Contains(jetpackArg)) {
		jetpack = !jetpack;
	}
	if(arg.Contains(raiseAccelArg)) {
		accelExponent++;
	}
	if(arg.Contains(lowerAccelArg)) {
		accelExponent--;
	}
	if(arg.Contains(resetAccelArg)) {
		accelExponent = 0;
	}

	// if(arg.Contains("%Vector")) {
	// 	TODO: parse the arg to get the vector
	// 	make it desiredVec
	// }

	return Vector3D.TransformNormal(moveVec, controllerMatrix);//turn movement into worldspace
}/**/


IMyShipController getController() {
	var blocks = new List<IMyShipController>();
	GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);
	if(blocks.Count < 1) {
		Echo("ERROR: no ship controller found");
		return null;
	}

	IMyShipController cont = /*(IMyShipController)*/blocks[0];
	int lvl = 0;
	bool allCockpitsAreFree = true;
	int prevLvl = 0;
	IMyShipController prevController = cont;
	bool hasReverted = false;
	for(int i = 0; i < blocks.Count; i++) {
		// only one of them is being controlled
		if(((IMyShipController)blocks[i]).IsUnderControl && allCockpitsAreFree) {
			prevController = cont;
			prevLvl = lvl;
			cont = ((IMyShipController)blocks[i]);
			lvl = 5;
		}//more than one is being controlled, it reverts to previous setting
		else if(((IMyShipController)blocks[i]).IsUnderControl && !allCockpitsAreFree && !hasReverted) {
			lvl = prevLvl;
			cont = prevController;
			hasReverted = true;
		}//has %Main in the name
		else if(((IMyShipController)blocks[i]).CustomName.IndexOf("%Main") != -1 && lvl < 4) {
			cont = ((IMyShipController)blocks[i]);
			lvl = 4;
		}//is ticked as a main cockpit
		else if(((IMyShipController)blocks[i]).GetValue<bool>("MainCockpit") && lvl < 3) {
			cont = ((IMyShipController)blocks[i]);
			lvl = 3;
		}//is set to control thrusters
		else if(((IMyShipController)blocks[i]).ControlThrusters && lvl < 2) {
			cont = ((IMyShipController)blocks[i]);
			lvl = 2;
		}
		else {
			cont = ((IMyShipController)blocks[i]);
			lvl = 1;
		}
	}
	return cont;
}

// checks to see if the nacelles have changed
public void checkNacelles(bool verbose) {
	var blocks = new List<IMyTerminalBlock>();
	echoV("Checking Nacelles...", verbose);

	GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(blocks);
	if(rotorCount != blocks.Count) {
		echoV($"Rotor count {rotorCount} is out of whack", verbose);
		nacelles = getNacelles();
		return;
	}
	blocks.Clear();

	GridTerminalSystem.GetBlocksOfType<IMyThrust>(blocks);
	if(thrusterCount != blocks.Count) {
		echoV($"Thruster count {thrusterCount} is out of whack", verbose);
		nacelles = getNacelles();
		return;
	}

	//TODO: check for damage
	echoV("Everything seems fine.", verbose);
}

void echoV(string s, bool verbose) {
	if(verbose) {
		Echo(s);
	}
}

public void init() {
	nacelles = getNacelles();
	controller = getController();
}
/*
IMyShipController getController() {
	var blocks = new List<IMyShipController>();
	GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);
	IMyShipController cont = blocks[0];

	foreach(IMyShipController c in blocks) {
		if(c.IsUnderControl) {
			return c;
		}
	}

	return cont;
}
*/
// G(thrusters * rotors)
// gets all the rotors and thrusters
List<Nacelle> getNacelles() {
	var blocks = new List<IMyTerminalBlock>();
	var nacelles = new List<Nacelle>();
	bool flag;

	rotorCount = 0;
	thrusterCount = 0;

	// get rotors
	GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(blocks);
	foreach(IMyMotorStator r in blocks) {
		rotorCount++;
		if(false/* TODO: set to not be in a nacelle */) {
			continue;
		}

		//if topgrid is not programmable blocks grid
		if(r.TopGrid.EntityId == Me.CubeGrid.EntityId) {
			continue;
		}

		// it's not set to not be a nacelle rotor
		// it's topgrid is not the programmable blocks grid
		Rotor rotor = new Rotor(r);
		nacelles.Add(new Nacelle(rotor));
	}
	blocks.Clear();

	// get thrusters
	GridTerminalSystem.GetBlocksOfType<IMyThrust>(blocks);
	foreach(IMyThrust t in blocks) {
		thrusterCount++;
		if(false/* TODO: set to not be in a nacelle */) {
			continue;
		}

		// get rotor it belongs to
		Nacelle nacelle = new Nacelle();// its impossible for the instance to be kept, this just shuts up the compiler
		IMyCubeGrid grid = t.CubeGrid;
		flag = false;
		foreach(Nacelle n in nacelles) {
			IMyCubeGrid rotorGrid = n.rotor.theBlock.TopGrid;
			if(rotorGrid.EntityId == grid.EntityId) {
				flag = true;// flag = 'it is on a rotor'
				nacelle = n;
				break;
			}
		}
		if(!flag) {// not on any rotor
			continue;
		}

		// it's not set to not be a nacelle thruster
		// it's on the end of a rotor
		Thruster thruster = new Thruster(t);
		nacelle.thrusters.Add(thruster);

	}
	blocks.Clear();

	foreach(Nacelle n in nacelles) {
		n.detectThrustDirection();
	}

	return nacelles;
}

void displayNacelles(List<Nacelle> nacelles) {
	foreach(Nacelle n in nacelles) {
		Echo($"\nRotor Name: {n.rotor.theBlock.CustomName}");
		// n.rotor.theBlock.SafetyLock = false;//for testing
		// n.rotor.theBlock.SafetyLockSpeed = 100;//for testing

		n.rotor.getAxis();
		// Echo($@"rotor axis: {Math.Round(n.rotor.wsAxis.Length(), 3)}");
		// Echo($@"rotor axis: {n.rotor.wsAxis.Length()}");
		// Echo($@"deltaX: {Vector3D.Round(oldTranslation - km.Translation.Translation, 0)}");

		Echo("Thrusters:");
		int i = 0;
		foreach(Thruster t in n.thrusters) {
			Echo($@"{i}: {t.theBlock.CustomName}");
			i++;
		}
	}
}

public class Nacelle {
	public String errStr;

	// physical parts
	public Rotor rotor;
	public List<Thruster> thrusters;
	public List<Thruster> activeThrusters;

	public Vector3D requiredVec = Vector3D.Zero;

	public float totalThrust = 0;

	public Nacelle() {}// don't use this if it is possible for the instance to be kept
	public Nacelle(Rotor rotor) {
		this.rotor = rotor;
		this.thrusters = new List<Thruster>();
		this.activeThrusters = new List<Thruster>();
		errStr = "";
	}

	// final calculations and setting physical components
	public void go() {
		errStr = $"\nactive thrusters: {activeThrusters.Count}";
		errStr += $"\nall thrusters: {thrusters.Count}";
		totalThrust = (float)calcTotalThrust(activeThrusters);
		if(false/*zeroG*/ /*&& requiredVec.Length() < zeroGFactor*/) {
			// rotor.setFromVec((controller.WorldMatrix.Down * zeroGFactor) - velocity);
		} else {
			rotor.setFromVec(requiredVec);
		}

		//set the thrust for each engine
		for(int i = 0; i < activeThrusters.Count; i++) {
			activeThrusters[i].setThrust(requiredVec * activeThrusters[i].theBlock.MaxEffectiveThrust / totalThrust);
		}
	}

	public float calcTotalThrust(List<Thruster> thrusters) {
		float total = 0;
		foreach(Thruster t in thrusters) {
			total += t.theBlock.MaxEffectiveThrust;
		}
		return total;
	}

	//true if all thrusters are good
	public bool validateThrusters() {
		bool flag = true;
		foreach(Thruster t in thrusters) {
			t.updateStatus();
			if(!t.isOn && t.isActive) {
				flag = false;
			}
		}
		return flag;
	}

	public void detectThrustDirection() {
		Vector3D engineDirection = Vector3D.Zero;
		Vector3D engineDirectionNeg = Vector3D.Zero;
		Vector3I thrustDir = Vector3I.Zero;
		Base6Directions.Direction rotTopUp = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Up);
		Base6Directions.Direction rotTopDown = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Down);

		// add all the thrusters effective power
		foreach(Thruster t in thrusters) {
			Base6Directions.Direction thrustForward = t.theBlock.Orientation.TransformDirection(Base6Directions.Direction.Forward); // Exhaust goes this way

			//if its not facing rotor up or rotor down
			if(!(thrustForward == rotTopUp || thrustForward == rotTopDown)) {
				// add it in
				var thrustForwardVec = Base6Directions.GetVector(thrustForward);
				if(thrustForwardVec.X < 0 || thrustForwardVec.Y < 0 || thrustForwardVec.Z < 0) {
					engineDirectionNeg += Base6Directions.GetVector(thrustForward) * t.theBlock.MaxEffectiveThrust * (t.isOn ? 1 : 0);
				} else {
					engineDirection += Base6Directions.GetVector(thrustForward) * t.theBlock.MaxEffectiveThrust * (t.isOn ? 1 : 0);
				}
			} else {
				// thrusters.Remove(t);
			}
		}

		// get single most powerful direction
		double max = Math.Max(engineDirection.Z, Math.Max(engineDirection.X, engineDirection.Y));
		double min = Math.Min(engineDirectionNeg.Z, Math.Min(engineDirectionNeg.X, engineDirectionNeg.Y));
		// errStr += $"\nmax:\n{Math.Round(max, 2)}";
		// errStr += $"\nmin:\n{Math.Round(min, 2)}";
		double maxAbs = 0;
		if(max > -1*min) {
			maxAbs = max;
		} else {
			maxAbs = min;
		}
		// errStr += $"\nmaxAbs:\n{Math.Round(maxAbs, 2)}";

		// TODO: swap onbool for each thruster that isn't in this
		if(Math.Abs(maxAbs - engineDirection.X) < 0.1) {
			errStr += $"\nengineDirection.X";
			if(engineDirection.X > 0) {
				thrustDir.X = 1;
			} else {
				thrustDir.X = -1;
			}
		} else if(Math.Abs(maxAbs - engineDirection.Y) < 0.1) {
			errStr += $"\nengineDirection.Y";
			if(engineDirection.Y > 0) {
				thrustDir.Y = 1;
			} else {
				thrustDir.Y = -1;
			}
		} else if(Math.Abs(maxAbs - engineDirection.Z) < 0.1) {
			errStr += $"\nengineDirection.Z";
			if(engineDirection.Z > 0) {
				thrustDir.Z = 1;
			} else {
				thrustDir.Z = -1;
			}
		} else if(Math.Abs(maxAbs - engineDirectionNeg.X) < 0.1) {
			errStr += $"\nengineDirectionNeg.X";
			if(engineDirectionNeg.X < 0) {
				thrustDir.X = -1;
			} else {
				thrustDir.X = 1;
			}
		} else if(Math.Abs(maxAbs - engineDirectionNeg.Y) < 0.1) {
			errStr += $"\nengineDirectionNeg.Y";
			if(engineDirectionNeg.Y < 0) {
				thrustDir.Y = -1;
			} else {
				thrustDir.Y = 1;
			}
		} else if(Math.Abs(maxAbs - engineDirectionNeg.Z) < 0.1) {
			errStr += $"\nengineDirectionNeg.Z";
			if(engineDirectionNeg.Z < 0) {
				thrustDir.Z = -1;
			} else {
				thrustDir.Z = 1;
			}
		} else {
			errStr += $"\nERROR (detectThrustDirection):\nmaxAbs doesn't match any engineDirection\n{maxAbs}\n{engineDirection}\n{engineDirectionNeg}";
			return;
		}

		// use thrustDir to set rotor offset
		Base6Directions.Direction rotTopForward = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Forward);
		Base6Directions.Direction rotTopLeft = rotor.theBlock.Top.Orientation.TransformDirection(Base6Directions.Direction.Left);
		rotor.offset = (float)Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopForward), (Vector3D)thrustDir));

		// disambiguate
		if(Math.Acos(rotor.angleBetweenCos(Base6Directions.GetVector(rotTopLeft), (Vector3D)thrustDir)) > Math.PI/2) {
			// rotor.offset += (float)Math.PI;
			rotor.offset = (float)(2*Math.PI - rotor.offset);
		}

		// put thrusters into the active list
		Base6Directions.Direction thrDir = Base6Directions.GetDirection(thrustDir);
			errStr += $"\nactiveThrusters count: {activeThrusters.Count}";
		if(activeThrusters.Count > 0) {
			activeThrusters.Clear();
		}
			errStr += $"\nactiveThrusters count: {activeThrusters.Count}";
		foreach(Thruster t in thrusters) {
			if(!t.isOn) {
				t.isActive = false;
				continue;
			}
			Base6Directions.Direction thrustForward = t.theBlock.Orientation.TransformDirection(Base6Directions.Direction.Forward); // Exhaust goes this way

			if(thrDir == thrustForward) {
				activeThrusters.Add(t);
				t.isActive = true;
			} else {
				t.isActive = false;
			}
		}
	}

}

public class Thruster {
	public IMyThrust theBlock;
	public string errStr = "";

	// stays the same when in standby, if not in standby, this gets updated to weather or not the thruster is on
	public bool isOn = true;

	// weather or not it is in the active list
	public bool isActive = false;

	public Thruster(IMyThrust thruster) {
		this.theBlock = thruster;
	}

	public void updateStatus() {
		errStr = "";
		isOn = theBlock.GetValue<bool>("OnOff");

		if(!theBlock.IsFunctional) {
			isOn = false;
		}
	}

	public void setThrust(Vector3D thrustVec) {
		// thrustVec is in newtons
		// double thrust = Vector3D.Dot(thrustVec, down);
		// convert to percentage
		double thrust = thrustVec.Length();
		thrust *= 100;
		thrust /= theBlock.MaxEffectiveThrust;

		thrust = (thrust > 100 ? 100 : thrust);
		thrust = (thrust < 0 ? 0 : thrust);
		errStr += $"thrust: {thrust}";
		// Program.Clamp(thrust, 100, 0);
		theBlock.SetValue<float>("Override", (float)thrust);// apply the thrust
	}

	public void setThrust(double thrust) {
		// thrust is in newtons
		// convert to percentage
		thrust *= 100;
		thrust /= theBlock.MaxEffectiveThrust;

		thrust = (thrust > 100 ? 100 : thrust);
		thrust = (thrust < 0 ? 0 : thrust);
		// Program.Clamp(thrust, 100, 0);
		theBlock.SetValue<float>("Override", (float)thrust);// apply the thrust
	}
}

public class Rotor {
	public IMyMotorStator theBlock;
	// don't want IMyMotorBase, that includes wheels

	public Vector3D wsAxis;// axis it rotates around in worldspace
	public float offset = 0;// radians

	public string errStr = "";

	public Rotor(IMyMotorStator rotor) {
		this.theBlock = rotor;
		getAxis();
	}

	// gets the rotor axis (worldmatrix.up)
	public void getAxis() {
		this.wsAxis = theBlock.WorldMatrix.Up;//this should be normalized already
		if(Math.Round(this.wsAxis.Length(), 6) != 1.000000) {
			errStr += $"\nERROR (getAxis()):\n\trotor up isn't normalized\n\t{Math.Round(this.wsAxis.Length(), 2)}";
			this.wsAxis.Normalize();
		}
	}

	// this sets the rotor to face the desired direction in worldspace
	// desiredVec doesn't have to be in-line with the rotors plane of rotation
	public void setFromVec(Vector3D desiredVec) {
		desiredVec = Vector3D.Reject(desiredVec, wsAxis);
		if(Vector3D.IsZero(desiredVec) || !desiredVec.IsValid()) {
			errStr += $"\nERROR (setFromVec()):\n\tdesiredVec is invalid\n\t{desiredVec}";
			return;
		}

		// angle between vectors
		float angle = -(float)Math.Acos(angleBetweenCos(theBlock.WorldMatrix.Forward, desiredVec));

		//disambiguate
		if(Math.Acos(angleBetweenCos(theBlock.WorldMatrix.Left, desiredVec)) > Math.PI/2) {
			angle = (float)(2*Math.PI - angle);
		}

		setPos(angle + (float)(offset/* * Math.PI / 180*/));
	}

	// gets cos(angle between 2 vectors)
	// cos returns a number between 0 and 1
	// use Acos to get the angle
	public double angleBetweenCos(Vector3D a, Vector3D b) {
		double dot = Vector3D.Dot(a, b);
		double Length = a.Length() * b.Length();
		return dot/Length;
	}

	// set the angle to be between 0 and 2pi radians (0 and 360 degrees)
	// this takes and returns radians
	float cutAngle(float angle) {
		while(angle > Math.PI) {
			angle -= 2*(float)Math.PI;
		}
		while(angle < -Math.PI) {
			angle += 2*(float)Math.PI;
		}
		return angle;
	}

	// move rotor to the angle (radians), make it go the shortest way possible
	public void setPos(float x)
	{
		theBlock.ApplyAction("OnOff_On");
		x = cutAngle(x);
		float velocity = maxRotorRPM;
		float x2 = cutAngle(theBlock.Angle);
		if(Math.Abs(x - x2) < Math.PI) {
			//dont cross origin
			if(x2 < x) {
				theBlock.SetValue<float>("Velocity", velocity * Math.Abs(x - x2));
			} else {
				theBlock.SetValue<float>("Velocity", -velocity * Math.Abs(x - x2));
			}
		} else {
			//cross origin
			if(x2 < x) {
				theBlock.SetValue<float>("Velocity", -velocity * Math.Abs(x - x2));
			} else {
				theBlock.SetValue<float>("Velocity", velocity * Math.Abs(x - x2));
			}
		}
	}

}