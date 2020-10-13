using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageable
{
	void TakeHit(int damage, DamageTypes.DamageType dmgtype, Vector3 hitPoint, Vector3 hitDirection, float pushForce, float pushRadius, int shooterNumber);

	void TakeDamage(int damage, DamageTypes.DamageType damageType);

	void FlashDamage();

    ObjectMaterial.Material GetSoundMaterial();
   
}
