using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class BulletNetwork : Bolt.EntityBehaviour<IBulletState> {

	// public enum BulletType { Kinematic, Physic, Raycast }; // Raycast = Insta hit | Kinematic = Moved by Translate, not efected by physics collisions with raycast | Dynamic = Physics, grenades atleast.
	// public BulletType bulletType; // choose carefully :)
	public Explosion.ExplosionSize explosionSize; // because we cant send prefab into explosion event, we are using enums as type of explosions. 
	public GameObject AudioLibraryGo; // has all the sfx for hit.
	private float speed = 1f;
	[SerializeField] private Rigidbody2D rb;
	private Sprite bulletSprite;
	public GameObject OnHitEffectGo; // Explosions (or add white pixel for the bullets?). Use Bolt entities.
	public GameObject smokeTrailGo; // this one is the one that is instantiated every x seconds behind the bullet.
	[SerializeField] private float explosionTime = 3f; // The grenade bounces until this timer is done or is hit the player. 
	[SerializeField] private float smokeTrailInEverySecond = 0.3f;
	private float explosionTimer; // saved in the beginning to be same as the timer. Used to be safe range hit self.
	private float timer;
	private bool animate = false;
	private bool ricochet = false;
	[SerializeField] private float ricochetPercentage = 0f; // ricochets are off until replicated correctly over network.
	private Vector2 prev = Vector2.zero;
	//public Collider2D lastHitCollider;
	private bool isDisabled = false;
	private bool isBlank = false;
	private bool isUnderWater = false;
	private bool isAlreadyHitSomething = false;

	public override void Attached()
	{
		timer = smokeTrailInEverySecond;
		explosionTimer = explosionTime;
		state.SetTransforms(state.BulletTransform, transform);
		var bulletData = (BulletToken)entity.AttachToken;

		//isBlank = (Random.Range(0f, 1f) < 0.15f); 

		if(entity.IsOwner)
		{
			if(rb == null)
				rb = GetComponent<Rigidbody2D>();
			state.Damage = bulletData.damage;
			state.ShooterNumber = bulletData.playerNumber;
			state.PushForce = bulletData.pushForce;
			state.PushRadius = bulletData.pushRadius;
			state.DamageRadius = bulletData.damageRadius;
			transform.Rotate(0,0, bulletData.rotation); 
				
			if(rb != null) {
					rb.AddRelativeForce(Vector2.right * bulletData.force, ForceMode2D.Impulse);		
			} else if (rb == null) {
				BoltLog.Error("No rigidbodies found in the body");
			}
		}
    }

	public override void SimulateOwner() {
		//physics bullets turn downwards, so we check it.
		if(!isDisabled)
		{
			if(!isUnderWater && Mathf.Abs(rb.velocity.sqrMagnitude) > 2f)  { // underwater no more turning down. Had flipping bug at bottom of the water + looks better like this.
				//Debug.LogWarning( Mathf.Abs(rb.velocity.sqrMagnitude) );
				Vector2 v = rb.velocity;
				float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
				transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
				// rb.sharedMaterial = null;
			}
		}
		// if we have an timer and its under x second before explosing, stop bouncying.
		if(explosionTime > 0f && explosionTimer < 0.33f) {
			rb.sharedMaterial = null;
		}

		if(explosionTime > 0f && explosionTimer <= 0 && isAlreadyHitSomething) { // grenade launcher, has a timer.
			if(isBlank == false) {
				Explode();
			} else {
				// fade and destroy.
			}
		} else if(explosionTime == 0 && isAlreadyHitSomething) { // rocket launcher, doesnt have a timer = insta explosion after hit.
			Explode();
			// BoltNetwork.Destroy(gameObject); // this is in the explode part.
		}
	}

	private void FixedUpdate() {
		if(isUnderWater == true)
		{
			rb.velocity = rb.velocity * 0.8f;
       		rb.angularVelocity = rb.angularVelocity * 0.8f;
			// rigidbody.velocity = rigidbody.velocity * 0.9;
		}

		if(explosionTimer > 0) {
			explosionTimer -= Time.deltaTime;
		}

		if(smokeTrailGo) {
			timer -= Time.deltaTime;
			if(timer <= 0) {
				Instantiate(smokeTrailGo, this.transform.position, Quaternion.identity);
				timer = smokeTrailInEverySecond;
			}
		}
	}


	/* void SpawnOnHitEffect(Vector2 normal)
	{
		//Vector3 reflectedDirection = Vector3.Reflect(normal, transform.right);
		//Debug.DrawRay(transform.position, reflectedDirection.normalized, Color.yellow, 10f);
		//Debug.DrawRay(transform.position, transform.right * 1f, Color.yellow, 10f);

		// todo still:
		// # Make the OnHitEffect Reflect the hit, like a billiard ball.
		// # Make the hiteffect spawn exactly at the bounds of the tile object, not few millimeters inside.
		// Now it is just hotfixed to be show behind the tile.
		//var effect = Instantiate(OnHitEffectGo, transform.position, Quaternion.FromToRotation(transform.position, transform.right * -1f) );
		//BoltNetwork.Instantiate(BoltPrefabs.BulletHitParticleEffect, transform.position, Quaternion.FromToRotation(transform.position, transform.right * -1f));
	} */

	// hit on player trigger.
	// note: Only granades have & use colliders. Normal bullets use raycasts!!
	// Triggers: Water, Player etc. 
	// Colliders: Others are colliders
    void OnTriggerEnter2D(Collider2D other) {
		if(entity.IsOwner) {

			if (other.tag == "Player") {
				// we dont want to destroy the bullet if its hit straight to ourself, before timer has gone
				if (/* other.GetComponent<PlayerParentOrganizer>().playerNumber == state.ShooterNumber &&  */explosionTimer > (explosionTime - 0.1f) && explosionTimer != 0) {
					BoltLog.Error("Explosive timer not ready yet");
					return;
				}
				
				// hotfix for bazooka, where timer is set to zero, dont hit ourself. Grenade launcher has a timer and it explodes anyway at 0.
				if(other.GetComponent<PlayerParentOrganizer>().playerNumber == state.ShooterNumber && explosionTimer == 0) {
					return;
				} 

				Explode();
				BoltNetwork.Destroy(gameObject);
			}

			if(other.tag == "Enemy") {
				if(state.ShooterNumber != -1) { // if shooter is not the A.I, exploded
					Explode();
				} else if(state.ShooterNumber == -1 && explosionTimer < (explosionTime - 0.5f) ) { // if its the A.I, check save zone so it wont explode right away.
					Explode();
				}
			}

			if(other.tag == "Water")
			{
				other.GetComponent<WaterDynamic>().WaterSplash(this.transform.position, 2f);
				Animator animator = GetComponent<Animator>();
				if(animator != null) { // Bazooka Missile.
					animator.SetTrigger("StopFlying");
				}
				rb.gravityScale = 1; // missile has 0 at the birth.
				rb.gravityScale /= 2f;
				isUnderWater = true;
				isBlank = true;
				StartCoroutine("DestroyAfterTime", 8f);
				// now we destroy the physic bullet aka grenade, but we could instantiate water grenade or something that doesnt explode and slowly goes to bottom.
				// and makes a slow silent sound (link a sinking boat which hits the bottom).
				// BoltNetwork.Destroy(gameObject);
			}
		}
    }

	void SpawnSmokeTrail() {
		if(smokeTrailGo != null)
			Instantiate (smokeTrailGo, transform.position, Quaternion.identity);
	}

	//explosion type hitting with -hard- colliders..
	void OnCollisionEnter2D(Collision2D collision) {
		isAlreadyHitSomething = true;
    }

	public void Explode() {
		if(isBlank)
			return;
		// make an explosion event and send this all to there.
		var explosionEvent = ExplosionEvent.Create(Bolt.GlobalTargets.Everyone);
		explosionEvent.ShooterNumber = state.ShooterNumber;
		explosionEvent.ExplosionPosition = transform.position;
		explosionEvent.Damage = state.Damage;
		explosionEvent.DamageRadius = state.DamageRadius;
		explosionEvent.PushForce = state.PushForce;
		explosionEvent.PushRadius = state.PushRadius;
		explosionEvent.ExplosionSizeEnumInt = (int)explosionSize;
		explosionEvent.Send();
		BoltNetwork.Destroy(gameObject);
	}

	// This is called from the bullet hit, the hit receives the material from the object it was hit.
	void PlayHitSound(ObjectMaterial.Material material) {
			// we might only play the default if those up ones are messed up. Lets hope it works.
			var audioLibraryToken = new AudioLibraryToken();
			audioLibraryToken.audioLibraryEnumInt = (int)material;
			//BoltLog.Error(material.ToString() + " and its int: " + (int)material);
			if(AudioLibraryGo != null)
				BoltNetwork.Instantiate(AudioLibraryGo, audioLibraryToken, transform.position, Quaternion.identity);
	}

	void HideBullet() {
		isDisabled = true;
		GetComponent<SpriteRenderer>().enabled = false;
		speed = 0f;
	}

	void DestroyBullet() {
		BoltNetwork.Destroy(gameObject);
	}

    IEnumerator DestroyAfterTime(float destroyAfterSeconds) {
        yield return new WaitForSeconds(destroyAfterSeconds);
        BoltNetwork.Destroy(gameObject); // disabled: Late comers had the object still at the scene and this causes problems.
    }

}
