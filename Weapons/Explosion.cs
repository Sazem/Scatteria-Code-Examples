using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Explosion : MonoBehaviour
{
    /// <summary>
    /// Explosion is the effect that comes from Grenades, missiles and such.
    /// Doesnt bring dmg to environment. That is done by bullet script before instatiating this
    /// This gives the mask to "destroy the sprite" and also gives physics to small objects.
    /// </summary>
    public int numberOfTheShooter = 0;
	public float damageRadius = 0f;
	public float pushRadius;
	private float pushForce;
	private int explosionDmg;
	private AudioClip audioClip;
	public enum ExplosionSize { SmallExplosion, MediumExplosion, BigExplosion, HugeExplosion,  EnormousExplosion, NuclearExplosion } // 6 types of sizes.
	public ExplosionSize explosionSize;
	private Animator animator;
	public ParticleSystem particles;
	float countdown;
	bool hasExploded = false;
	public ExplosionData[] explosionData;

	void Start () {
		animator = GetComponent<Animator>();
		SetAnimatorToHaveCorrectExplosion(); // the animation will be played depending on of the enum..
		Explode();
	}

	private void OnBecameVisible() {
		Debug.LogWarning("Explosion, do the ripple");	
	}

	public void OnDrawGizmosSelected() {
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, pushRadius);
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, damageRadius);
	}

	public void SetExplosion(int _explosionIntEnum, int _dmg, float _damageRadius, float _pushForce, float _pushRadius, int _shooterNumber) 
	{
		explosionDmg = _dmg;
		damageRadius = _damageRadius;
		// GetComponent<AudioSource>().clip = clip;
		explosionSize = (ExplosionSize)_explosionIntEnum;
		numberOfTheShooter = _shooterNumber;
		pushRadius = _pushRadius;
		pushForce = _pushForce;
	}

	void SetAnimatorToHaveCorrectExplosion() {

		// this is still unfinished, but anyway you choose enum that what type is the explosion and the 
		// animation is played depending on that. Masking object comes from the animation event.
		switch (explosionSize)
		{
			case (ExplosionSize.SmallExplosion):
			{
				animator.SetTrigger("SmallExplosion");
				break;
			}
			case (ExplosionSize.MediumExplosion):
			{
				animator.SetTrigger("MediumExplosion");
				break;
			}
			case (ExplosionSize.BigExplosion):
			{
				animator.SetTrigger("BigExplosion");
				break;
			}
			case (ExplosionSize.HugeExplosion):
			{
				Debug.LogError("Huge Explsosion: Not yet implented");
				break;
			}
			case (ExplosionSize.EnormousExplosion):
			{
				Debug.LogError("Enourmous Explsosion: Not yet implented");
				break;
			}
			case (ExplosionSize.NuclearExplosion):
			{
				Debug.LogError("Nuclear: Not yet implented");
				break;
			}
		}
	}

	public void Explode() {
		if(pushForce == 0) // if no custom values set, get default values from class arraylist
			pushForce = GetFromData(explosionSize).pushForce;
		if(pushRadius == 0)
			pushRadius = GetFromData(explosionSize).pushRadius;
		if(audioClip == null)
			audioClip = GetFromData(explosionSize).audioClip;
		if(explosionDmg == 0)
			explosionDmg = GetFromData(explosionSize).damage;
		if(particles == null)
			particles = GetFromData(explosionSize).particleSystem;
		if(damageRadius == 0)
			damageRadius = GetFromData(explosionSize).damageRadius;


		Collider2D[] colliders = Physics2D.OverlapCircleAll (transform.position, pushRadius); // push colliders.
		foreach (Collider2D collider in colliders) {
			Rigidbody2D rb = collider.GetComponent<Rigidbody2D> ();
			if (rb != null) {
				
				IPushable pushableObject = collider.GetComponent<IPushable>();
				if(pushableObject != null) {
					pushableObject.ReceivePush( pushForce, 
												pushRadius, 
												transform.position, 
												(collider.transform.position - transform.position).normalized ); // bullet forces here
					Debug.LogWarning("Pushed: " + collider.name + " pos: " +  transform.position + " force: " + pushForce + " radius: " + pushRadius);
				}
			}
		}

		Collider2D[] damageColliders = Physics2D.OverlapCircleAll(transform.position, damageRadius);
		foreach (Collider2D col in damageColliders) {
			IDamageable damageableObject = col.GetComponent<IDamageable>();
			if(damageableObject != null) {
				damageableObject.TakeHit(explosionDmg, 
											DamageTypes.DamageType.Explosion, 
											transform.position, 
											(col.transform.position - transform.position).normalized,
											pushForce, 
											pushRadius, 
											numberOfTheShooter); 
				Debug.LogWarning("Damage given to: " + col.gameObject.name + ". Dmg: " + explosionDmg);
			} else if (col.tag == "Tiles") { // also is server and we create a callback to replicate it to everyone.
		
				Tilemap tilemap;
				tilemap = col.GetComponent<Tilemap> ();

				// vectors to send to destroy the next tiles.
				// consider in the future, to change this into radius. Maybe with int. 2 = two tiles in y or x.. etc
				Vector2[] vectors = new Vector2[8];
				vectors [0] = new Vector2 (-1, -1); // Numper 1
				vectors [1] = new Vector2 (0, -1);
				vectors [2] = new Vector2 (1, -1);
				vectors [3] = new Vector2 (-1, 0); // numper 4, fifth is in the middle so its missing. Would be Vector.zero.
				vectors [4] = new Vector2 (1, 0); // numper 6
				vectors [5] = new Vector2 (-1, 1); // numper 7
				vectors [6] = new Vector2 (0, 1); // numper 8
				vectors [7] = new Vector2 (1, 1); // numper 9 

				DestroyTile (transform.position, tilemap); // this is the explosion position itself. before this added, it sometimes left one block in center undestroyed.
				
				foreach(Vector2 vector in vectors) {
					Vector2 worldVector = transform.TransformPoint (vector);
					DestroyTile (worldVector, tilemap);
				}

				World.Instance.UpdatePathFindingNodes(); // A.I to get updated pathfinding.
			} 
		}
			
		

		if (particles != null) {
			ParticleSystem partikkelit = Instantiate (particles, transform.position, Quaternion.identity) as ParticleSystem;
		}
		
	
		hasExploded = true;
		//AudioSource.PlayClipAtPoint(GetComponent<AudioSource>().clip, transform.position); // we play the explosion sound and get rid of it.
		GetComponent<AudioSource>().Play();
		Invoke("HideObject", this.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).length); // we hide the object after animation has played.
		Destroy(gameObject, 3f /* GetComponent<AudioSource>().clip.length */); // this destroy the object after animation is done.
	} // Explode ends

	public void InstantiateMaskObject(Sprite sprite)
	{
		GameObject explosionMaskingObj = Resources.Load("Effects/MaskingObject", typeof(GameObject)) as GameObject;
		SpriteMask spriteMask = explosionMaskingObj.GetComponent<SpriteMask>();
		spriteMask.sprite = sprite; // because this method is triggered from animation event, this should have the mask from correct time..
		explosionMaskingObj = Instantiate(explosionMaskingObj, transform.position, Quaternion.identity);
	}

	private void HideObject() {
		GetComponent<SpriteRenderer>().enabled = false;
	}

	void DestroyTile(Vector2 targetPos, Tilemap tilemap) {
		if(tilemap == null)
			return;
		// we check if the point had a tile
		if (tilemap.GetTile ( tilemap.WorldToCell(targetPos) ) != null) {
			// we set null into exploted tile.
			tilemap.SetTile (tilemap.WorldToCell (targetPos), null);

			// lets instantiate a destroyed bg into the spot.. also mask it.
			Vector3Int gridPos = tilemap.WorldToCell(targetPos); // we make the explosion pos into Vector3Int..
			Vector2 gridPosInWorld = tilemap.CellToWorld (gridPos); // then make it Vector2 with gridpoints..
			InstantiateDestroyedBackground (gridPosInWorld);

		}
	}

	void InstantiateDestroyedBackground(Vector2 worldPos) {
		Instantiate (Resources.Load ("Tiles/defaultTileBackground"), worldPos, Quaternion.identity);
	}

	public ExplosionData GetFromData(ExplosionSize size) {
		foreach (ExplosionData data in explosionData)
		{
			if(size == data.size)
				return data;
		}
		return null;
	}

	[System.Serializable]
	public class ExplosionData {
		public Explosion.ExplosionSize size;
		public int pushForce;
		public float pushRadius;
		public int damage;
		public float damageRadius;
		public AudioClip audioClip;
		public ParticleSystem particleSystem;
	}

}
