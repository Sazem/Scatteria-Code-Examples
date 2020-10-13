using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// local bullet made by events.
public class Bullet : MonoBehaviour {

	public GameObject audiolibraryGo;
	private float speed = 1f;
	public GameObject onHitEffectGo; // Explosion? or a white hit effect on the ground.
	public LayerMask layerMask;
	Vector2 prev = Vector2.zero;
	public Collider2D lastHitCollider;
	private bool isDisabled = false;

	private int shooterNumber;
	private int dmg;
	private float pushForce;
	private float pushRadius;
	private bool ricochet = false; 

	public void SetBullet(int _shooterNumber, float _speed, int _dmg, float _pushForce, float _pushRadius, bool _ricochet) {
		
		this.shooterNumber = _shooterNumber;
		dmg = _dmg;
		speed = _speed;
		pushForce = _pushForce;
		pushRadius = _pushRadius;
		ricochet = _ricochet;
		// if(ricochet == true) // if a ricochet is a true, we turn on the the layer so bullet will hit it.
		// 	layerMask |= 1 << LayerMask.NameToLayer(layerName: "RicochetItems"); // we add the ricochet layer.
	}

	// Update is called once per frame
	void Update () {

		transform.Translate(Vector2.right * speed * Time.deltaTime /* BoltNetwork.FrameDeltaTime */);

		if(prev != Vector2.zero) {
			RaycastHit2D hit = Physics2D.Linecast(prev, transform.position, layerMask); // this is the hit check game currently.
			if(hit.collider != lastHitCollider)
			{
				//if(OptionsData.debugMode) Debug.Log("bullet hit into" + hit.collider.name);
				lastHitCollider = hit.collider;
				if(hit.collider != null) // this fixed the ricochet null error.
					Hit(hit);
			}
		}
		prev = transform.position;
	}

	public void Hit(RaycastHit2D hit) {
		// we dont hit ourself...
		if( hit.collider.CompareTag("Player") )  {
			if( hit.collider.GetComponent<BoltEntity>().GetState<IPlayerState>().PlayerNumber == shooterNumber) {
				return;  // return if hit ourself.
			}
		}	

		// do we ricochet if we pass. 
		if(hit.collider.tag == "RicochetItems") // this random check is made with changing the physics layers.
		{
			// # make a ricochet sound, depending what we hit?
			// # slow bullet a little? or give longer trail, or extra trail to show it was hit this kind of object.
			// # maybe sometimes even destroy the bullet straight here.
			if(ricochet == true) {
				PlayHitSound( ObjectMaterial.Material.Ricochet );
				SpawnOnHitEffect(hit.normal); // make the hit effect on the.. object.
				// # TODO IMPORTANT: When enabled, make a new fireEvent.  we are going to have too many events because
				// We might have to random a few random angles in the shooter class, so all of them will follow same route.
			}
			return;
		}

		IPushable pushableObject = hit.collider.GetComponent<IPushable>();
		if(pushableObject != null) {
			pushableObject.ReceivePush(pushForce, pushRadius, transform.position, transform.right);
		}

		IDamageable damageableObject = hit.collider.GetComponent<IDamageable>();
		if(damageableObject != null) {
			// play hit audio
			PlayHitSound(damageableObject.GetSoundMaterial() );
			damageableObject.TakeHit(dmg, DamageTypes.DamageType.Bullet, transform.position, transform.right, pushForce, pushRadius, shooterNumber);
		}

		BrokeItemProperties brokeItemProperties = hit.collider.GetComponent<BrokeItemProperties>();
		if(brokeItemProperties != null)	
			return; // we dont want the local objects destroy the bullet

		if(hit.collider.tag == "Water") {
			hit.collider.GetComponent<WaterDynamic>().WaterSplash(transform.position, 1f);
			speed *= 0.6f;
			return; // We dont want the bullet to be destroyed if hit the water.
		}

		if(hit.collider.tag == "Tiles") {
			// DestructibleTileMap destructibleTileMap = hit.collider.gameObject.GetComponent<DestructibleTileMap>();
			// if(destructibleTileMap != null) {
			// 	Vector2 hitDirection = (hit.point - (Vector2)transform.position).normalized;
			// 	destructibleTileMap.Damage(dmg, hit.point + (hitDirection.normalized * 0.5f));
			// }

			PlayHitSound(ObjectMaterial.Material.Dirt);
			SpawnOnHitEffect(hit.normal);
		}

		Destroy(this.gameObject); // bullet did hit the final, now destroy it.
	}

	// This is called from the bullet hit, the hit receives the material from the object it was hit.
	void PlayHitSound(ObjectMaterial.Material material)
	{
			// we might only play the default if those up ones are messed up. Lets hope it works.
			var audioLibraryToken = new AudioLibraryToken();
			audioLibraryToken.audioLibraryEnumInt = (int)material;
			//BoltLog.Error(material.ToString() + " and its int: " + (int)material);
			// eli taas pitää vaihtaa boltista normaaliin.
			if(audiolibraryGo != null) // itun bolt ttaastatagad
				BoltNetwork.Instantiate(audiolibraryGo, audioLibraryToken, transform.position, Quaternion.identity);
	}

	void SpawnOnHitEffect(Vector2 normal) {
		// instantiate the hit effect here.
		if(onHitEffectGo != null)
		{
			Instantiate(onHitEffectGo, transform.position, Quaternion.FromToRotation(transform.position, transform.right * -1f));
		}
	}




}
