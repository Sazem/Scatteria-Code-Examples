using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;

public class AI_SoldierHealthSystem : Bolt.EntityBehaviour<IPlayerState>, IDamageable {
    public int startingHealth = 70;
    private int currentHealth;
    public int CurrentHealth { get; set; }
    public DamageFlashEffect[] flashEffects;
    [SerializeField] TextEventEffect textEventEffect;
    public ObjectMaterial.Material material;

    public GameObject hitSplatter;
    private AudioSource audioSource;
    public AudioClip[] takeDamageAudioClips;
    public float minTimeBetweenHurtAudio = 1f;
    private float hurtAudioTimer; 
    
    public event Action DamageTakenEvent;
    public event Action DeathEventLocal; // not bolt, normal event. For A.I Spawners etc.

    private int whoKilledNumber;

    public override void Attached() {
        if(entity.IsOwner) {
            state.PlayerHealth = startingHealth;
            state.IsDead = false;
            state.PlayerNumber = -1;
        }
    }

    public void FlashDamage() {
        foreach (DamageFlashEffect damageFlashEffect in flashEffects) {
            damageFlashEffect.Flash();
        }
    }

    public void TakeHit(int damage, DamageTypes.DamageType damageType, Vector3 hitPoint, Vector3 hitDirection, float pushForce, float pushRadius, int shooterNumber) {
        
        if(damageType == DamageTypes.DamageType.Bullet) 
           IsThereTileBehindEnemy(hitPoint, hitDirection);
       
        if(entity.IsOwner == false)
            return;
        
        if(state.IsDead == false) {
            PlayRandomDamageAudio();
            if(hitSplatter != null) 
                BoltNetwork.Instantiate( hitSplatter, hitPoint, Quaternion.FromToRotation(Vector2.right, hitDirection) );
            
            if(state.PlayerHealth < damage && !state.IsDead)
                whoKilledNumber = shooterNumber;

            if(textEventEffect != null) {
                TextEventEffect textEvent = Instantiate(textEventEffect, hitPoint, Quaternion.identity);
                textEvent.TextEffect("-" + damage, state.PlayerColor, hitDirection);
            }
            TakeDamage(damage, damageType);
        }
    }

    public void TakeDamage(int damage, DamageTypes.DamageType damageType) {

        if(state.IsDead == true)
            return;
            
        state.PlayerHealth -= damage;
        FlashDamage();

        if(DamageTakenEvent != null)
            DamageTakenEvent();

        if(state.PlayerHealth <= 0) {
            state.PlayerHealth = 0;
            state.IsDead = true;
            Die(damageType);
        }
    }
    void Die(DamageTypes.DamageType damageType) {
        if(DeathEventLocal != null)
            DeathEventLocal(); // at least spawners / game master will be listening to this. 
        
        var deathEvent = DeathEvent.Create();
        deathEvent.DeathPosition = transform.position;
        deathEvent.DeathPrefabColor = state.PlayerColor;
        deathEvent.DeathEnumInt = (int)damageType;
        deathEvent.LookingRight = GetComponent<CharacterControls>().isFacingRight;
        deathEvent.WhoDiedNumber = -1; // A.I will be now -1.
        deathEvent.WhoKilledNumber = whoKilledNumber;
        deathEvent.BodyDecayTime = UnityEngine.Random.Range(OptionsData.bodyDecayTimeMin, OptionsData.bodyDecayTimeMax);
        deathEvent.Send(); // send to GameManagerEventListener.cs
       
        if(OptionsData.debugMode) { // green target gross that was used to debug enemy movement target, parent was set to null so didnt destryo with others.
            AI_SoldierDebug debug = GetComponentInChildren<AI_SoldierDebug>();
            if(debug != null) {
                Destroy(debug.finalTargetSprite);
            }
        }

        BoltNetwork.Destroy(this.gameObject);
    }

    public ObjectMaterial.Material GetSoundMaterial() {
        return material;
    }

    void PlayRandomDamageAudio() {
        Debug.LogWarning("Random DamageAudio not implented yet", this);
    }

    // if tile behind enemy, send blood effect on the tile. 
    void IsThereTileBehindEnemy(Vector2 hitPos, Vector2 hitDirection) {
        LayerMask mask = (1 << 8); // ground
        RaycastHit2D hit =  Physics2D.Raycast(hitPos, hitDirection, 2f, mask);
        if(hit != false) {
            if(hit.collider.CompareTag("Tiles")) {
                SendBlood(hit.point, hit.normal, hit.collider.gameObject.GetComponent<Tilemap>(), hitDirection);
            }
        }
    }

    void SendBlood(Vector2 bloodPos, Vector2 normal, Tilemap tilemap, Vector2 direction) {
		Vector2 insideTheTile = bloodPos + direction * 0.01f; // this sends the position little bit forwards toward the hit pos.
		Vector3Int gridPos = tilemap.WorldToCell(insideTheTile); // get that inside positions tile..
		Vector2 gridPosInWorld = tilemap.CellToWorld (gridPos); // and return its world pos.
        bool isBetween = false;
        // original testing scripts is called BloodTester.cs..
        Vector3 rotation = Vector3.zero;
        if(normal == Vector2.up) {
            // no need to rotate the vector, in this case its 0
            //Debug.Log("SendBlood: normal up: Check tiles between, above?"); // correct.
            // isBetween = tilemap.HasTile(new Vector3Int(gridPos.x, gridPos.y + 1, 0));
        } else if(normal == Vector2.down) {
            rotation = new Vector3(0, 0, 180);
            // isBetween = tilemap.HasTile(new Vector3Int(gridPos.x, gridPos.y - 1, 0));
        } else if(normal == Vector2.left) {
            rotation = new Vector3(0, 0, 90); // left
        } else if(normal == Vector2.right) {
            rotation = new Vector3(0, 0,270); // right
        } else { // Non correct normal
            isBetween = true;
        }
        
        if(!isBetween) {
            // adjustPos because the pivot.
            Vector2 adjustedPos = new Vector2(gridPosInWorld.x + 0.5f, gridPosInWorld.y + 0.5f); // 0.5f to go center of the tile.
            GameObject bloodObject = Instantiate(Resources.Load ("Effects/bloodOnBGwall"), adjustedPos, Quaternion.Euler(rotation)) as GameObject;
        }
    }

    void OnTriggerEnter2D(Collider2D col) {
        if (col.tag == "Water") { // problem was here that the A.I was stuck on water.
            // A.I went to water, but player cant see him. So teleport him closest point to player.
            if(GameMaster.Instance.IsVisibleForPlayerCamera(this.gameObject) == false) {
                StopAllCoroutines();
                this.transform.position = GameMaster.Instance.GetSpawnPoint();
                // give target to the player too.. get more action.
            } else {
                StartCoroutine("OnWater"); // kill the enemy for staying too long in water.
            }
        } 
    }

    IEnumerator OnWater() {
        Debug.LogError("Enemy is on water");
        yield return new WaitForSeconds(15f);
        TakeDamage(200, DamageTypes.DamageType.Water); 
    }
}
