using UnityEngine;

[CreateAssetMenu(fileName = "AmmoScriptableObject", menuName = "Scatteria/New Ammo Type", order = 3)]
public class AmmoType : ScriptableObject {
	public int ammoId; // this will be reference between bolt events etc. 
	public string Name;
	public int maxAmmo;
	public GameObject bulletGameObject;
	public GameObject AmmoDropObject; // Reference for the AmmoPickUp object. Spawner has a reference to this. 
	public Color outlineColor;
}
