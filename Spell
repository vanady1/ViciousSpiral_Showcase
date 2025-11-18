using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public abstract class Spell : MonoBehaviourPun
{
    [Header("Basic settings")]
    public string spellName;
    public int manaCost;
    public int charges;
    public Sprite UIIcon;
    public GameObject VFXDisplay;
    public bool isActiveSpell = false;
    public float coolDown;

    [Header("Pattern recognition")]
    public List<int> pattern;

    [Header("Casting references")]
    public Transform shootPoint;

    public SpellSlotSystem spellSlotSystem;
    public PlayerMana playerMana;
    public PhotonView playerPhotonView;
    public PlayerMovement playerMovement;

    protected virtual void Start()
    {
        playerMana = transform.root.GetComponent<PlayerMana>();
        playerPhotonView = transform.root.GetComponent<PhotonView>();
        spellSlotSystem = GetComponent<SpellSlotSystem>();
        shootPoint = GetComponentInParent<Camera>()
            .GetComponentInChildren<WeaponSwitcher>()
            .GetComponentInChildren<MagicRings>(true)
            .transform;
        playerMovement = transform.root.GetComponent<PlayerMovement>();
    }

    protected bool IsManaEnough()
    {
        if (playerMana == null)
            return false;

        return playerMana.mana >= manaCost;
    }

    public void UseStoredSpell()
    {
        if (TryCastSpell())
        {
            CastSpell();
        }
    }

    public bool TryStoreSpell()
    {
        if (IsManaEnough() && spellSlotSystem.AvailableSlots() >= 1)
        {
            playerMana.TryUseMana(manaCost);
            spellSlotSystem.StoreSpell(this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extra validation before casting (line of sight, cooldown etc.).
    /// Override in concrete spells.
    /// </summary>
    public virtual bool TryCastSpell()
    {
        return true;
    }

    protected abstract void CastSpell();
}
