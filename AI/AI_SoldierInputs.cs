using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI_SoldierInputs : Bolt.EntityBehaviour<IPlayerState> {
	
	private CharacterControls characterControls; // A.I use the same script than the player.
	private PlayerParentOrganizer playerParentOrganizer;
	private Inventory inventory;
	private bool gettingDown = false;

	private void Awake() {
		characterControls = GetComponent<CharacterControls>();
		playerParentOrganizer = GetComponent<PlayerParentOrganizer>();
		inventory = playerParentOrganizer.inventory;
	}

	public override void Attached() {
		state.SetTransforms(state.PlayerTransform, transform);
		state.SetAnimator( GetComponent<Animator>() );
		state.SetTransforms(state.WeaponTransform, playerParentOrganizer.inventory.shooter.transform);
		state.AddCallback("PlayerColor", ChangeColor);
		state.AddCallback("Angle", ChangeAngle);
		
		if(entity.IsOwner) {
			state.PlayerColor = playerParentOrganizer.playerColor;
		}

		if(entity.IsOwner) {
			NextWeapon(); // If A.I receives 2nd weapon at GameMaster, this activates it. If not.. it actually does nothing, goes back zero aka pistol.
		}
		state.OnFire = () => { playerParentOrganizer.inventory.shooter.MultiplayerShoot(entity); };
	}

	public void Move(Vector2 direction, bool jumped, bool aiming) {
		characterControls.Move(direction.x, jumped, PlayerInputs.ControllerType.KeyboardOnly, aiming);
	}

	public void AimAngle(int angle) {

		angle = angle % 360;
		angle = angle < 0 ? angle + 360 : angle;
		
		characterControls.angle = angle;
		characterControls.Direction(angle);
	}

	public void ResetAim() {
		// set angle to 0, or 270 what ever.
		characterControls.angle = characterControls.isFacingRight ? 0 : 180;
	}

	public void NextWeapon() {
		characterControls.NextWeapon();
	}

	public void PreviousWeapon() {
		characterControls.PreviousWeapon();
	}

	public void ChangeToPistol() {
		// hardcoded: the pistol is zero index!
		characterControls.ChangeWeapon(0);
	}

	public void StandUp() {
		characterControls.StandUp();
	}

	public void Crouch() {
		characterControls.Crouch(PlayerInputs.ControllerType.Controller);
	}

	public bool IsCrouched() {
		return characterControls.isCrouched;
	}

	public void Reload() {
		characterControls.Reload();
	}

	public void Shoot() {
		// if out of ammo, change to weapon that has ammo
		if(inventory.AmmosLeftOnAmmoType(inventory.shooter.weapon.ammoType) > 0 || inventory.shooter.weapon.hasInfiteAmmo) {
			// Debug.Log("A.I Shoot: " + inventory.AmmosLeftOnAmmoType(inventory.shooter.weapon.ammoType));
			// shoot
			if(inventory.shooter.fireFrame + inventory.shooter.FireFrameRate <= BoltNetwork.ServerFrame && /* state.Clip > 0 && */ inventory.shooter.canShoot) {
				playerParentOrganizer.inventory.shooter.fireFrame = BoltNetwork.ServerFrame;
				playerParentOrganizer.inventory.shooter.triggerReleased = false;
				state.Fire();
			}
		} else {
			Debug.Log("Ai Ran out of ammo, changing to pistol", this);
			
			ChangeToPistol();
		}
	}

	public void Aim(bool aim) {
		characterControls.aimingMode = aim;
	}

	public void GetDownLadder() {
		if(gettingDown == false) {
			StartCoroutine("GetDownLadderWithReset");
		}
	}

	public IEnumerator GetDownLadderWithReset() {
		gettingDown = true;
		characterControls.PressingDown();
		yield return new WaitForSeconds(1.5f);
		characterControls.PressingDownReleased();
		gettingDown = false;
	}

	// callbacks for network replication.
	void ChangeColor() {
		playerParentOrganizer.playerColor = state.PlayerColor;
		playerParentOrganizer.SetPlayerColors();
	}

	void ChangeAngle() {
		characterControls.angle = state.Angle;
	}

	void Aiming() {
		characterControls.aimingMode = state.Aiming;
	}

}

