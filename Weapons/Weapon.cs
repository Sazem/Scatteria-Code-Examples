using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName="Scatteria/New Weapon")]
public class Weapon : ScriptableObject {

	public string weapon;
	public int weaponId; // number used with Spawners etc (cant replicate scriptableobjects over network). Weapon is saved to GameManager array and its matched from there by id.
	public Color weaponOutlineColor; // now used with TextEventEffect.textColor.. when pickup happens etc.
	public Sprite weaponSprite;
	public Sprite crosshairSprite;
	public Sprite[] shootAnim;
	public Sprite[] boltActionAnim;
	public Sprite[] reloadAnim;
	public Sprite weaponSpriteHUD;
	public Sprite bulletSpriteHUD;
	//public Sprite weaponPickUpSprite; // without hands. // its in the prefab because bolt dont like Scriptable objects
	public Sprite weaponShotDropSprite; // weapon that shot and drop after use. ex: bazooka black muzzle and used.
	public Sprite weaponWithoutHands; // use for the turrets!
	// This is the type of the weapon. 
	// Bolt action: Shot once and then reload sound (bolt action, shotgun etc). Also reloading adds only one bullet per at the time.
	// Singleshot: Pistols and such. Shoots only the shooting sound. No pump sounds and player has to release the trigger to shoot again.
	// Automatic: ratatatatatata!
	public enum WeaponType {boltAction, singleShot, automatic};
	public WeaponType weaponType;
	public Vector2 muzzlePosition;	

	[Header("Weapon Ammo")]
	public bool isPhysicalBullet = false; // this was made for tilefront ray check, that players can shoot throu tiles, but it stopped the grenade instantiation too.
	public int minDamage; // we are going to randomise from these two values the final damage.
	public int maxDamage;
	public float pushForce; // only push the object with this force.
	public float pushRadius; // explosions have bigger, think like circle area. Pushes the objects around.
	public float damageRadius; // this is explosion specific, area of the explosion that sends damage. Smaller than pushRadius
	public float bulletSpeed; // Initial speed of the bullet, units per second. Remember later to see: var speed = rigidbody.velocity.magnitude;
	[Range(0f, 45f)]
	public float bulletSpeedRange; // if we have multiple projectiles, this is the range. It adds and discounts the amount at Range(bulletspeed - bulletspeedrange, bulletSpeed)
	public bool hasInfiteAmmo = false; // infinite ammo, doh!
	public bool dropWeaponAfterAmmoDepleted = false; 
	public int maxBulletsPerClip; // Example rifle 5, Smg 32 etc.
	// public int maxAmmo; // maximum ammo the player can have ammo. // this moved to ammoType, easier to handle throu there.
	public GameObject bulletPrefab; // used now for the network physic bullets
	public GameObject hylsyPrefab; // also this most likely could be done in the AmmoType Scriptable Obj.
	public GameObject emptyMagazinePrefab; // if this isnt empty, everytime player reloads instantiates it into the scene.
	public AmmoType ammoType;

	[Header("Weapon Sounds")]
	public AudioClip[] ShootSounds;
	public AudioClip reloadSound;
	public AudioClip endReloadSound; // Lock and load!
	public float reloadPitch = 1f;
	[Range(0f, 1f)]
	public float endReloadVolume = 1f;
	public AudioClip emptyClickSound;
	[MinMaxRange(0f, 2f)]
	public RangedFloat pitch;
	
	[Header("Weapon Specs")]
	public float shootingSpeed = 0.5f; // speed how often player can shoot the weapon. Rifle slow, smg hight. Aka cooling time.
	public float reloadSpeed = 1.5f; // How fast player reloads the weapon
	public int projectileAmount = 1; // ex: rifle 1, shotgun 6(?)

	[Range(0f, 45f)]
	public float spreadRange; // lets specify the values later. Angles, max 45.
	[Range(0f, 1f)]
	public float ricochetPercent = 0f; // without aiming, aiming cuts the ricochet amount, by half? lets see. 1 = 100% aka everytime hits into obj and ricochets.
	// also movement speed may increase / decrease this one. Show that in the crosshair!
	public float zoomOutDistance; // camerazoom. Sniper alot. Smg almost nothing.
	public float aimTargetDistance; // how far we move the cameraTarget (midpoint) between Player and Crosshair. Smg 1f, rifle 5f?
	[Range(0.00f, 0.05f)]
	public float kickBackForce; // how much screenshakes after shot and player is moved backwards.

	[Header("Weapon Melee")]
	public int meleeDamage;
	public int meleeSpeed; 
	public AudioClip meleeAttackSound;
	// public float meleeRange; // I think melee range is the same for all
	//public GameObject meleeEffect; // White Wiuh animation, or do we make this in the weapon animation?
}
