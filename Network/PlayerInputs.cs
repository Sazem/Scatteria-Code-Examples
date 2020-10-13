using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InControl;

public class PlayerInputs : Bolt.EntityBehaviour<IPlayerState> {
	// references
	CharacterControls characterControls;
	PlayerParentOrganizer playerParentScript;
	Inventory inventory;
	public float keyboardAimSpeedMultiplier = 120f; // these are now hardcoded down with aim mode.
	public float angleInFloat;
	public Camera playerCamera;

	// inputs
	public PlayerActions Actions { get; set; }
	public enum ControllerType {KeyboardWithMouse, Controller, KeyboardOnly, KeyboardOnlyPlayerTwo};
	public ControllerType controllerType;
	ControllerType lastControllerType; // for Debugging. We can change the controls on fly.
	public string deviceName;
	public float x, y;
	public int angle = 0;
	public float deadzone = 0.25f;
	public Vector2 input = new Vector2(0, 0);
	public float magnitude;
	public bool playerIsUsingAimStick = false;

	private void Awake() {
		characterControls = GetComponent<CharacterControls>();
		playerParentScript = GetComponent<PlayerParentOrganizer>();
		inventory = playerParentScript.inventory;
	}
	
	public override void Attached() {
		state.SetTransforms( state.PlayerTransform, transform); 
		state.SetAnimator( GetComponent<Animator>() );
		state.SetTransforms( state.WeaponTransform, playerParentScript.inventory.shooter.transform );
		state.AddCallback("PlayerColor", ChangeColor); // changed above here. Maybe no the colors will change in the mp.
		state.AddCallback("Angle", ChangeAngle);

		// PlayerToken token, currently comes only at sp mode. 
		var playerData = (PlayerToken)entity.AttachToken;

		if(entity.IsOwner) {
			if(playerData != null && BoltNetwork.IsSinglePlayer) { // if we have the playerData, aka sp mode.
				//BoltLog.Warn("We have a player data token, most likely we are at sp mode.");
				state.PlayerColor = playerData.playerColor; // we set the data that came from registery
				state.PlayerNumber = playerData.playerNumber; // especially this one.
				PlayerStats stat = OptionsData.GetPlayerStats(playerData.playerNumber);
				SetDevice(stat.actions, stat.controllerType);
				this.gameObject.name = this.gameObject.name.Replace("(Clone)", "_" + state.PlayerNumber.ToString() );
			} else {
				state.PlayerColor = OptionsData.playerColor; // else we are network mode and everybody receives their color from their data.
			}
			
			if(BoltNetwork.IsClient)
				state.PlayerNumber = MultiplayerPlayerRegistery.localPlayer.playerNumber; // we received this as event from server when connected.
			else if(BoltNetwork.IsServer && OptionsData.splitscreenMode == false) { // server and sp mode will come here.
				state.PlayerNumber = 1; // dont know if hard coding is good or bad here. anyway it should be always 1.
				if(OptionsData.debugMode) Debug.LogError("Player receives nro 1 because: sp mode: " + OptionsData.singlePlayerMode.ToString() + ". Splitscreenmode: " + OptionsData.splitscreenMode.ToString());
			}
				
			playerParentScript.playerColor = state.PlayerColor;
			playerParentScript.playerNumber = state.PlayerNumber;
			characterControls.playerNumber = state.PlayerNumber;

			playerCamera = playerParentScript.GetPlayerCamera();
			playerParentScript.SetCameraAndHudEvents(); // also assign the correct hud to the inventory. MP we search the type, sp we cant.
			playerParentScript.isCameraOwner = true;

			if(BoltNetwork.IsSinglePlayer) {
				playerParentScript.inventory.ChangeCrosshairLayer(state.PlayerNumber);
			}
			
			inventory.weapons = GameManager.instance.GetStartingWeapons();
		}
		//gameObject.name = "Player_" + state.player + "_Id: " + state.PlayerNumber;

		state.OnFire = () => // callback for the trigger, so the player will make the instantiating of the bullet.
		{
			playerParentScript.inventory.shooter.MultiplayerShoot(entity);
		};

	}

	public void Update() {
		if(OptionsData.pause && BoltNetwork.IsSinglePlayer) { // pause set, dont rotate flip player..
			return;
		}

		// // if we are at sp mode or online, we can change the controller schema on the fly.
		// if(Actions != null) {
		// 	// check if the last input device is not the same as current one.
		// 	// if its different, change it to Controller or Keyboard+Mouse.
		// 	if(OptionsData.singlePlayerMode || BoltNetwork.IsSinglePlayer == false) {
		// 		Debug.Log(InputManager.ActiveDevice);
		// 		Debug.Log(Actions.ActiveDevice);
		// 	}
		// }

		if(entity.IsOwner && OptionsData.inputsDisabled == false) {
			if(lastControllerType != controllerType) {
				Actions = null;
				Debug.LogError("Actions were nulled");
			}
		
			// if bindings are null, assign bindings to them.
			if(Actions == null) {
				print("Actions are null");

				switch(controllerType)
				{
					case ControllerType.KeyboardWithMouse:
						Actions = ControlManager.Instance.keyboardListener;
						break;
					case ControllerType.Controller:
						Actions =  ControlManager.Instance.joystickListener;
						break;
					case ControllerType.KeyboardOnly:
						Actions = ControlManager.Instance.keyboardOnlyListener;
						break;
					case ControllerType.KeyboardOnlyPlayerTwo:
						Actions =  ControlManager.Instance.keyboardOnlyPlayerTwoListener;
						break;
						
					default:
						Actions = ControlManager.Instance.keyboardListener;
						break;
					
				}
				lastControllerType = controllerType;
			}

			// we update depending what controller type we have.
			switch(controllerType)
			{
				case ControllerType.KeyboardWithMouse:
					UpdateKeyboardAndMouseSpecific();
					break;
				case ControllerType.Controller:
					UpdateControllerSpecific();
					break;
				case ControllerType.KeyboardOnly:
					UpdateKeyboardOnlySpecific();
					break;
				case ControllerType.KeyboardOnlyPlayerTwo:
					UpdateKeyboardOnlySpecific(); // the style is the same in both of these.
					break;
			}
			UpdateCommon(); // the contrors that are similar, example Jump & shoot etc.
		}
		
	} // end of update.

	public void UpdateControllerSpecific() {
		// int angle = 0;
		x = Actions.Aim.X;
		y = Actions.Aim.Y;

		// from third-helix.com thumbstick deadzone: googlaa!
		input = new Vector2(x, y);
		magnitude = input.magnitude;
		if (input.magnitude < deadzone) {
			input = Vector2.zero;
			x = 0;
			y = 0;
		}

		// tämä fastfix oli oikeastan aika roskaa. nyt toi päivitys on tolla booleanilla mikä menee character controlleihin.
		// if ( x > 0f || y > 0f ) { // was: x != 0f && y != 0f
		// 	angle = Mathf.RoundToInt( Mathf.Atan2(y, x) * Mathf.Rad2Deg); // this calculates the angle from the x, y inputs.
		// }

		// we check, if player is using the aim (rightStick), so we will flip the player if he isnt overriding the flip
		if(Mathf.Abs(x) > 0 || Mathf.Abs(y) > 0) {
			angle = Mathf.RoundToInt( Mathf.Atan2(y, x) * Mathf.Rad2Deg); // this calculates the angle from the x, y inputs.
			playerIsUsingAimStick = true;
			angle = angle % 360;
			angle = angle < 0 ? angle + 360 : angle;
			characterControls.angle = angle;
			characterControls.Direction(angle);
		} else {
			playerIsUsingAimStick = false;
		}
		
		state.Angle = angle;
		// joystick get down, dont know if works?
		if (Actions.UpAndDownLadder < -0.85f)
		{
			characterControls.PressingDown();
		}
		else if (Actions.UpAndDownLadder > -0.84f)
		{
			characterControls.PressingDownReleased();
		}
		characterControls.Move(Actions.Move.Value, Actions.Jump.IsPressed, controllerType, playerIsUsingAimStick); // liikutetaan hahmoa, plus toisena on hypyn boolean joka hypäyttää hahmoa toisessa scriptsisä.
	}

	public void UpdateKeyboardAndMouseSpecific() 
	{
		// if(playerCamera == null) // this did instantiate the camera at menu when returning from the game.
		// 	playerParentScript.GetPlayerCamera();
		var mouse = Input.mousePosition;
		var screenPoint = playerCamera.WorldToScreenPoint(transform.position);
		var offset = new Vector2(mouse.x - screenPoint.x, mouse.y - screenPoint.y);
		int angle = Mathf.RoundToInt(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
		angle = angle % 360;
		angle = angle < 0 ? angle + 360 : angle;
		characterControls.angle = angle;
		characterControls.Direction(angle);
		characterControls.Move(Actions.Move.Value, Actions.Jump.IsPressed, controllerType, false);
		state.Angle = angle;
		// this we might have to change into commons()!!!		
		if (Actions.ClimpDownKeyboard.IsPressed) {
			characterControls.PressingDown();
		}
		if (Actions.ClimpDownKeyboard.WasReleased) {
			characterControls.PressingDownReleased();
		}

	}

	public void UpdateKeyboardOnlySpecific() 
	{
		if(characterControls.angle > 360)
			characterControls.angle = 0;

		angleInFloat = (float)characterControls.angle; // needed the angle to be float.. 

		if(!Actions.ActionKey.IsPressed) { // we need to check if the changeweapon&reload isnt pressed, because we r using aimDown for reload.
			// we are checking the max angles. So the aiming stops at max/min = 90/-90 from both 
			if(Actions.AimUp.IsPressed && characterControls.isFacingRight) { // right and upper
				angleInFloat += keyboardAimSpeedMultiplier * Time.deltaTime; // smoothing the aiming, because was a mess before!
				characterControls.angle = (int)angleInFloat;
				 //+= keyboardSpeedMultiplier * Time.deltaTime;
				if(characterControls.isFacingRight && characterControls.angle >= 90 && characterControls.angle < 270)
					characterControls.angle = 90;
			}
			else if(Actions.AimDown.IsPressed && characterControls.isFacingRight) { // right and below
				angleInFloat -= keyboardAimSpeedMultiplier * Time.deltaTime;
				characterControls.angle = (int)angleInFloat;
				// if below zero..
				if(characterControls.angle < 0) { // we set the angle to 360.
					characterControls.angle = 360;
				}
				if(characterControls.angle < 270 && characterControls.angle > 91)
				{
					characterControls.angle = 270;
				}
			}
			else if(Actions.AimUp.IsPressed && characterControls.isFacingRight == false) // left and upper
			{
				angleInFloat -= keyboardAimSpeedMultiplier * Time.deltaTime;
				characterControls.angle = (int)angleInFloat;
				if(characterControls.angle <= 90)
					characterControls.angle = 90;
			} 
			else if(Actions.AimDown.IsPressed && characterControls.isFacingRight == false) // left and below
			{
				angleInFloat += keyboardAimSpeedMultiplier * Time.deltaTime;
				characterControls.angle = (int)angleInFloat;
				if(characterControls.angle > 270)
					characterControls.angle = 270;
			}
		}
	
		// AimDOwn and changeweapons = reload.
		if(Actions.ActionKey.IsPressed && Actions.AimDown.WasPressed) 
			characterControls.Reload();
		
		if(Actions.ActionKey.IsPressed && Actions.MoveLeft.WasPressed) 
			characterControls.PreviousWeapon();

		if(Actions.ActionKey.IsPressed && Actions.MoveRight.WasPressed) 
			characterControls.NextWeapon();


		// we apply the movement, but if the changeweapon is pressed, we stop the player. So the player changes the weapons without moving horizontally.
		if(Actions.ActionKey.IsPressed) {
			characterControls.Move(0, Actions.Jump.IsPressed, controllerType, false);
		} else
		{
			characterControls.Move(Actions.Move.Value, Actions.Jump.IsPressed, controllerType, Actions.Aiming.IsPressed); // this also has the aim override. 
		}
	}

	public void UpdateCommon() 
	{
		if (Actions.Reload.IsPressed)
		{
			characterControls.Reload();
		}
		if (Actions.Shoot.IsPressed)
		{
			if(OptionsData.shootInputDisabled) // networkscore board disables canvas, so the player cant shoot while at the scoreboard.
				return;

			if(inventory.shooter.fireFrame + inventory.shooter.FireFrameRate <= BoltNetwork.ServerFrame && /* state.Clip > 0 && */ inventory.shooter.canShoot) {
				playerParentScript.inventory.shooter.fireFrame = BoltNetwork.ServerFrame;
				playerParentScript.inventory.shooter.triggerReleased = false;
				state.Fire();
			}
			//characterControls.Shoot();
		}
		if(Actions.Shoot.WasReleased) {
			// for autoreload. Player has to release the trigger before autoreload happens.
			characterControls.TriggerWasReleased();
		}

		if (Actions.Jump.IsPressed) {
			characterControls.Jump();
		}

		if (Actions.Jump.WasReleased) {
			//characterControls.jumpIsPressed = false;
		}
		if (Actions.NextWeapon.WasPressed)
		{
			characterControls.NextWeapon();
		}
        if(Actions.PreviousWeapon.WasPressed)
        {
            characterControls.PreviousWeapon();
        }
		// if (Actions.ChangeGrenade.WasPressed)
		// {
		// 	characterControls.NextGrenade();
		// }
		if (Actions.Aiming.WasPressed)
		{
			characterControls.AimMode(true);
			keyboardAimSpeedMultiplier = 70f;
		}
		if(Actions.Aiming.WasReleased) 
		{
			characterControls.AimMode(false);
			keyboardAimSpeedMultiplier = 120;
		}
		if (Actions.Crouch.WasPressed)
		{
			characterControls.Crouch(controllerType);
		}
		// if (Actions.ThrouGrenade.IsPressed)
		// {
		// 	characterControls.ChargeGrenade();
		// }
		// if (Actions.ThrouGrenade.WasReleased)
		// {
		// 	characterControls.ThrouGrenade();
		// }
	}

	public void SetDevice(PlayerActions actions, ControllerType type) {
		this.Actions = actions;
		//deviceName = actions.Device.Name; // tämä paska sitten antoi testeissä null errorin eikä pystytty pelaan 4 pelaajalla.. kiitos thanks! näppäimet != device aka null.
		controllerType = type;
		lastControllerType = controllerType;
	}

	void ChangeColor() {
		playerParentScript.playerColor = state.PlayerColor;
	}
	void ChangeAngle() {
		characterControls.angle = state.Angle;
		characterControls.Direction(state.Angle);
	}
	void Aiming() {
		characterControls.aimingMode = state.Aiming;
	}
}
