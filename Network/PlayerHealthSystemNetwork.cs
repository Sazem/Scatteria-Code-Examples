using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

public class PlayerHealthSystemNetwork : Bolt.EntityBehaviour<IPlayerState>, IDamageable {
   
	[SerializeField] private int startingHealth = 100;
    [SerializeField] private ObjectMaterial.Material material; // fleshhit, or similar.
	[SerializeField] private TextEventEffect textEventEffect;
    //public int currentHealth; // this is saved on the bolt network state and replicated over network.
    
    [SerializeField] private GameObject bloodSplatter;
	private AudioSource audioSource;
    public AudioClip[] waterDmgAudioclips;
    private CharacterControls controller;
	public GameObject deathPrefab;
 
    public int playerNro;
	public bool playerDead = false;
    public bool playerKilledByWater = false;
    public bool playerFeetOnWater = false;
    public bool playerHeadUnderWater = false;
    
    private PlayerParentOrganizer playerParentScript;
    public HUD2019 hud;

    public event Action DamageTaken;
    public event Action DeathEventLocal; // this is singleplayer event. Player dies => GameMaster gets signal.
    // // flash event for camera
    // public delegate void DamageFlash();
    // public event DamageFlash OnDamageFlash;

    public float waterTimer = 0f; // if the water is poisoneus.. start timer and start giving dmg every tick.
    public bool waterTimerRunning = false;
    private WaterDynamic waterScript; // reference when player enters the water, nulled when exits.

    private float holdAirTickTime = 2f; // how often a tick comes
    private float underWaterTimer = 2f; // times, uses that one above.
    private bool underWaterTimeRunning = false; 
    private int maxTicksPlayerCanBeUnderWater = 5; // how many ticks player can take before die.  
    private int ticksAlreadyTaken = 0; // how many ticks currently.

    private int whoKilledNumber;

    public override void Attached() {
        if(entity.IsOwner) {
            state.PlayerHealth = startingHealth;
            state.IsDead = false;
            playerFeetOnWater = false;
        }
        // call back for damage, or health change.. to update the hud.
        state.AddCallback("PlayerHealth", UpdateHud);
        state.AddCallback("IsDead", StartDisableAndDestroy);
        audioSource = GetComponent<AudioSource>();
        controller = GetComponent<CharacterControls>();
        playerParentScript = GetComponentInParent<PlayerParentOrganizer>();
    }

    private void Start() {
        if(hud == null && entity.IsOwner && BoltNetwork.IsSinglePlayer == false)
           hud = (HUD2019)FindObjectOfType(typeof(HUD2019));
        UpdateHud();
        PlayerHeadAboveWater();
    }

    public void UpdateHud() {
        if(hud != null && entity.IsOwner) {
            hud.UpdateHealth(state.PlayerHealth);
        }
    }

    public void SetPlayerNumberAndHud(int playerNumber, HUD hud) {
        this.playerNro = playerNumber;
       // this.hud = hud;
    }
    
    public override void SimulateOwner() {
        // Start timer if player feet is on the water and its poisonoues, comes from trigger enter.
        if(waterTimerRunning == true)
        {
            waterTimer -= BoltNetwork.FrameDeltaTime; // discount the timer
            if(waterTimer <= 0) // When it hits zero, give dmg.
            {
                if(playerFeetOnWater == true && waterScript != null)
                {
                    TakeDamage(waterScript.waterData.damagePerTick, DamageTypes.DamageType.Water);
                    waterTimer = waterScript.waterData.timeTick; // and reset the timer back
                }
            }
        }
        
        // head underwater..
        if(underWaterTimeRunning == true)
        {
            // plan: maybe add bubbles or some sort hud to display how long player can be underwater.
            underWaterTimer -= BoltNetwork.FrameDeltaTime;
            if(underWaterTimer <= 0)
            {
                if(playerHeadUnderWater == true)
                {
                    ticksAlreadyTaken++;
                    underWaterTimer = holdAirTickTime;
                    // make a sound fx.
                    if(ticksAlreadyTaken == maxTicksPlayerCanBeUnderWater)
                    {
                        // Die(0, damageType.Water);
                        TakeDamage(100, DamageTypes.DamageType.Water);
                    }
                }
            }
        }

        // if(OptionsData.debugMode && Input.GetKeyDown(KeyCode.L)) {
        //     if(playerDead) {
        //         Debug.Log("Spawning Player manually");
        //         GameManager.instance.gameEnded = false;
        //         GameManager.instance.SpawnPlayer(state.PlayerNumber);
        //     }
        // }

    }

	void Die(DamageTypes.DamageType damageType) {
		controller.playerDead = true;
        state.IsDead = true;
		controller.playerDead = true;
		//audioSource.PlayOneShot(deathAudioClip, 1.0f);
		controller.enabled = false;

        // instantiate the death prefab. make to BoltNetwork...
	    //GameObject deathPrefab = Instantiate(deathPrefab.gameObject, parentPos.position, Quaternion.identity) as GameObject;
        var deathEvent = DeathEvent.Create();
        deathEvent.DeathPosition = transform.position;
        deathEvent.DeathPrefabColor = state.PlayerColor;
        deathEvent.DeathEnumInt = (int)damageType;
        deathEvent.LookingRight = playerParentScript.characterControls.isFacingRight; //state.LookingRight;
        deathEvent.WhoDiedNumber = state.PlayerNumber;
        deathEvent.WhoKilledNumber = whoKilledNumber;
        deathEvent.BodyDecayTime = UnityEngine.Random.Range(OptionsData.bodyDecayTimeMin, OptionsData.bodyDecayTimeMax);
        deathEvent.Send(); // send to GameManagerEventListener.cs, or thats the one listening

        if(DeathEventLocal != null) 
            DeathEventLocal.Invoke();
        DamageTaken = null; // reset the events 

        if(playerParentScript.cameraScript != null)
            playerParentScript.cameraScript.PlayerUnderWater(false, Color.clear);
       
        if(entity.IsOwner) {
            if(OptionsData.singlePlayerMode || OptionsData.debugMode) { 
                GameMaster.Instance.PlayerDied(); // just remove live from the game.
                GameObject.FindObjectOfType<MessageBox>().SpawnMessageStart();
                if(GameMaster.Instance.isGameOn == true || OptionsData.isTutorial) 
                    GameManager.instance.SpawnPlayer(state.PlayerNumber);
                return;
            }
            
            GameObject.FindObjectOfType<MessageBox>().SpawnMessageStart();
            if(BoltNetwork.IsSinglePlayer) {
                GameManager.instance.SpawnPlayer(state.PlayerNumber); // sp mode needs player numbers, at the start they get it from registery.
            } else {
                GameManager.instance.SpawnPlayer();
            }
        }

	}

	void OnTriggerEnter2D(Collider2D col) {
        if (col.tag == "Water") {
            playerFeetOnWater = true;
            waterScript = col.GetComponent<WaterDynamic>();
            if(waterScript.waterData.timeTick > 0 && waterScript != null)
            {
                waterTimer = waterScript.waterData.timeTick;
                waterTimerRunning = true;
            }
            // if (state.IsDead == false)
            // {
            //     state.PlayerHealth = 0;
            //     //HealthSlider.value = currentHealth;
            
            //     //audioSource.clip = waterDmgAudio;
            //     //audioSource.Play();
            //     if(playerParentScript.DebugPlayer) // Dont remove the player with water at debug mode.
            //         return;
            //     //audioSource.PlayOneShot(waterDmgAudio, 0.5F);
            //     //AudioSource.PlayClipAtPoint(waterDmgAudio, this.transform.position);
            //     playerKilledByWater = true;
            //     //Debug.Log("died by water");
            //     if (state.PlayerHealth <= 0)
            //     {
            //         state.PlayerHealth = 0;
            //         if(GameManager.instance != null)
            //             GameManager.instance.playerDiedByWater (playerNro);
            //         Die();		
            //     }
            // }
        }
	}

    private void OnTriggerExit2D(Collider2D other) {
        if(other.tag == "Water")
        {
            playerFeetOnWater = false;
            waterScript = other.GetComponent<WaterDynamic>();
            if(waterScript.waterData.timeTick > 0 && waterScript != null)
            {
                waterTimerRunning = false;
                waterTimer = 0;
                waterScript = null;
            }
        }
    }

    // Network code uses this
    public void TakeHit(int damage, DamageTypes.DamageType damageType, Vector3 hitPoint, Vector3 hitDirection,  float hitForce, float pushRadius, int shooterNumber)
    {
        if(damageType == DamageTypes.DamageType.Bullet) { // every local sends the blood themself.
            IsThereTileBehindPlayer(hitPoint, hitDirection);
        }

        if(entity.IsOwner == false) // currently this is only at local. So only owner will receive the dmg.
            return;

        if(state.IsDead == false)
        {
            //audioSource.clip = takeDmgAudioclips[ UnityEngine.Random.Range(0, takeSeriesDamageAudioclips.Length) ];
            //audioSource.Play(); // we play the random sound.
            
            // Audio clips to play, think the logic throu.. if health less than x, make the guy yell harder..
            // or if received more than x damage at once?
            if(state.PlayerHealth < 50)
            {

            }
            // blood effect..
            if(bloodSplatter != null)
            {
               // BoltLog.Info("Blood should be spawned");
                BoltNetwork.Instantiate( bloodSplatter, hitPoint, Quaternion.FromToRotation(Vector2.right, hitDirection) );
            }

            if(state.PlayerHealth < damage && !state.IsDead) // hack.. kind of. Gives point to player if he will die.
            {
                // so player died, we will save the killer number to use in the death event later on.
                whoKilledNumber = shooterNumber;
                // //GameManagerNetwork.instance.RemoveScore(entity);
                // if(state.PlayerNumber == shooterNumber) // if we shoot ourself, minus score.
                //     GameManager.instance.RemoveScore(entity);
                // else
                //     GameManager.instance.AddScore(shooterNumber); // someone else, add score.
            
            }

            if(textEventEffect != null) {
                TextEventEffect tEffect = Instantiate(textEventEffect, hitPoint, Quaternion.identity);
                Color damageColor = BoltNetwork.IsSinglePlayer ? Color.red : state.PlayerColor;
                tEffect.TextEffect("-" + damage, damageColor, hitDirection);
            }

            TakeDamage(damage, damageType); // change this to damage type too..
        }
    }

    public void TakeDamage(int dmg, DamageTypes.DamageType damageType)
    {
        if(state.IsDead == true)
            return;
            
        state.PlayerHealth -= dmg;
        
        if(DamageTaken != null)
            DamageTaken();

        if(damageType == DamageTypes.DamageType.Water)
        {
            // play water dmg audio..
            if(waterDmgAudioclips.Length > 0)
            {
                audioSource.PlayOneShot(
                    waterDmgAudioclips[UnityEngine.Random.Range(0, waterDmgAudioclips.Length)]
                );
            }
        }

        if(state.PlayerHealth <= 0)
        {
            state.PlayerHealth = 0;
            state.IsDead = true;
            // Die...
            Die(damageType); // WE HAVE TO CHANGE THIS TO THE dmgType version!!...
        }
    }

    public void AddHealth(int healthAmount) {
        // if owner add health.
        Debug.Log(healthAmount + " health added to", this);
    }

    public ObjectMaterial.Material GetSoundMaterial()
    {
        return material;
    }

    void StartDisableAndDestroy() {
        StartCoroutine( DisableAndDestroy() );
    }

    // couldnt use bolt entity at the spawn (gm), so disable + delay to fix the issue.
    IEnumerator DisableAndDestroy()
    {
        // # disable everything of the player object.
        playerParentScript.DisableEverythingOnPlayer();
        // # find the messageboard and give message to it (only locally)
        //      For the respawn messages to a single player you typically create an 
        //      event with overload that takes a connection,
        //      and send to the connection of that player

        // Edited now, lets make this all locally. 
        /* 
        BoltConnection connection = MultiplayerPlayerRegistery.GetMultiplayerPlayerObject(this.entity).connection;
        var spawnMessage = MessageEvent.Create(connection);
        spawnMessage.IsSpawnMessage = true;
        spawnMessage.Send();
        */
        
        // GameObject.FindObjectOfType<MessageBox>().SpawnMessageStart();
        yield return new WaitForSeconds(OptionsData.spawnDelay + .1f); // give a little extra time so the bolt entity isnt destroyed before spawn.
        
        // destroy the player gameobject entirely.
        BoltNetwork.Destroy(gameObject);
    }

    public void PlayerHeadUnderWater()
    {
        if(playerParentScript.isCameraOwner) 
        {
            // - Camera underwater sprite
            if(waterScript != null)
                playerParentScript.cameraScript.PlayerUnderWater(true, waterScript.waterData.underWaterCameraColor);
            // - Mixer Snapshot => underwater
            AudioManager.Instance.SnapshopUnderWater();
        }
        // - Start calculating the breath / Air left
        underWaterTimeRunning = true;
        playerHeadUnderWater = true;
        // - gravity / speed changes.
    
    }

    public void PlayerHeadAboveWater() {

       if(playerParentScript.isCameraOwner) {
        // - Camera underwater sprite off
            if(playerParentScript.cameraScript != null)
                playerParentScript.cameraScript.PlayerUnderWater(false, Color.clear);
            // - Mixer snapshot => Default
            AudioManager.Instance.SnapshotMain();
       }
        // - Reset breath timer
        playerHeadUnderWater = false;
        underWaterTimeRunning = false;
        underWaterTimer = holdAirTickTime;
        ticksAlreadyTaken = 0; 
        if(maxTicksPlayerCanBeUnderWater - ticksAlreadyTaken == 1) // aka last breath.
        {
            // play breath sound.
        }
        // - Gravity / Speed = normal.
    }

    void IsThereTileBehindPlayer(Vector2 hitPos, Vector2 hitDirection) {
		LayerMask mask = (1 << 8);  // mask, so we only check grounds now.
		                            // check if tile is there (or other object in future)
		RaycastHit2D hit = Physics2D.Raycast(hitPos, hitDirection, 1.5f, mask);
		Debug.DrawRay (hitPos, hitDirection, Color.red, 15f);
		// if the hit is not null and it hits Tile... send blood.
		if (hit != false) {
			if (hit.collider.tag == "Tiles") {
				// Send blood
				SendBlood (hit.point, hit.normal, hit.collider.gameObject.GetComponent<Tilemap> (), hitDirection);
			}
		}
	}

	void SendBlood(Vector2 bloodPos, Vector2 normal, Tilemap tilemap, Vector2 direction) {
		Vector2 insideTheTile = bloodPos + direction * 0.01f; // this sends the position little bit forwards toward the hit pos.
		Vector3Int gridPos = tilemap.WorldToCell(insideTheTile); // we get that inside positions tile..
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

    // needed for IDamageable but not in use for players currently
    public void FlashDamage()
    {
       // throw new NotImplementedException();
       // Maybe implent character sprite flash in the future, or not?
    }
}
