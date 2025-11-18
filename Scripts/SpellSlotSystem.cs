using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;
using TMPro;

public class SpellSlotSystem : MonoBehaviourPun
{
    [Header("Slots & spells")]
    public List<SpellSlot> spellSlots = new List<SpellSlot>();
    public List<Spell> availableSpells = new List<Spell>();
    public int maxSlots = 5;

    [Header("Input")]
    private KeyCode castKey = KeyCode.Mouse1;
    private KeyCode useSlotKey = KeyCode.Mouse0;

    [Header("Casting UI")]
    public GameObject SpellCastList;
    public GameObject slotPrefab;
    public Transform slotsParent;
    public Sprite emptySlotIcon;

    [Header("State flags")]
    public bool isTextingOrInMenu = false;
    public bool CanCast = false;
    public bool isDrawing = false;

    [Header("Pattern preview (right panel)")]
    public Image patternSlotIcon;
    public TextMeshProUGUI spellName;
    public TextMeshProUGUI spellCost;
    public TextMeshProUGUI spellCostTitle;
    public string SpellCostTitleText = "cost: ";

    [Header("Active spell VFX")]
    public GameObject activeSpellVFX;
    public Transform shootPoint;

    private PlayerStateController playerStateController;
    private PlayerMovement playerMovement;
    private int currentActiveSlot = 0;

    [Header("Instructions toggle")]
    public Button instructionsHideButton;
    public Button instructionsShowButton;

    private void Start()
    {
        playerStateController = transform.root.GetComponent<PlayerStateController>();
        playerMovement = transform.root.GetComponent<PlayerMovement>();

        SetupAvailableSpells();
        CreateSlotObjects();
        UpdateSlotIcons();

        // At start nothing is shown on the right pattern panel
        HidePatternUI();
    }

    private void Update()
    {
        if (!CanCast || isTextingOrInMenu)
            return;

        if (Input.GetKeyDown(castKey) || Input.GetKeyDown(KeyCode.LeftControl))
            ToggleCastingMode();

        if (!isDrawing)
        {
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (scrollInput > 0f) SelectNextFilledSlot();
            else if (scrollInput < 0f) SelectPreviousFilledSlot();

            if (Input.GetKeyDown(useSlotKey))
                spellSlots[currentActiveSlot]?.UseSlot();

            // Select slot by numeric keys 1–9
            for (int i = 0; i < Mathf.Min(spellSlots.Count, 9); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    if (spellSlots[i].currentSpell != null)
                    {
                        SetActiveSlot(i);
                    }
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.H))
                ToggleInstructions();
        }
    }

    public void ToggleInstructions()
    {
        if (instructionsHideButton != null && instructionsHideButton.isActiveAndEnabled)
            instructionsHideButton.onClick.Invoke();
        else if (instructionsShowButton != null)
            instructionsShowButton.onClick.Invoke();
    }

    public void ToggleCastingMode()
    {
        if (SpellCastList == null) return;

        var curr = playerStateController != null
            ? playerStateController.GetCurrentState()
            : PlayerStateController.PlayerState.NORMAL;

        if (curr == PlayerStateController.PlayerState.CASTING)
        {
            // Exit casting mode: switch state and hide right-side pattern panel
            playerStateController?.SetPlayerState(PlayerStateController.PlayerState.NORMAL);
            HidePatternUI();
        }
        else
        {
            // Do not allow casting while not grounded
            if (playerMovement != null && !playerMovement.IsGrounded)
            {
                Debug.Log("Cannot cast while in air!");
                return;
            }

            playerStateController?.SetPlayerState(PlayerStateController.PlayerState.CASTING);
            // Enter casting mode: initially nothing is shown on the right panel
            HidePatternUI();
        }
    }

    private void SetupAvailableSpells()
    {
        availableSpells.Clear();
        Spell[] spells = GetComponents<Spell>();
        foreach (Spell spell in spells)
        {
            availableSpells.Add(spell);
        }
    }

    private void CreateSlotObjects()
    {
        spellSlots.Clear();

        for (int i = 0; i < maxSlots; i++)
        {
            GameObject slotObject = Instantiate(slotPrefab, slotsParent);
            SpellSlot spellSlot = slotObject.GetComponent<SpellSlot>();

            spellSlot.spellSlotSystem = this;
            spellSlot.shootPoint = shootPoint;
            spellSlot.emptyIcon = emptySlotIcon;
            spellSlot.ClearSlot();

            spellSlots.Add(spellSlot);
        }

        currentActiveSlot = 0;
        if (spellSlots.Count > 0)
            spellSlots[0].SelectSlot();
    }

    private void SelectNextFilledSlot()
    {
        if (spellSlots.Count == 0) return;

        int initialSlot = currentActiveSlot;

        do
        {
            currentActiveSlot = (currentActiveSlot + 1) % maxSlots;
        }
        while (spellSlots[currentActiveSlot].currentSpell == null &&
               currentActiveSlot != initialSlot);

        SetActiveSlot(currentActiveSlot);
    }

    private void SelectPreviousFilledSlot()
    {
        if (spellSlots.Count == 0) return;

        int initialSlot = currentActiveSlot;

        do
        {
            currentActiveSlot = (currentActiveSlot - 1 + maxSlots) % maxSlots;
        }
        while (spellSlots[currentActiveSlot].currentSpell == null &&
               currentActiveSlot != initialSlot);

        SetActiveSlot(currentActiveSlot);
    }

    private void SetActiveSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= spellSlots.Count) return;

        foreach (SpellSlot slot in spellSlots)
            slot.DeselectSlot();

        currentActiveSlot = slotIndex;
        spellSlots[currentActiveSlot].SelectSlot();
        UpdateSlotIcons();
        UpdateSpellVFX();
    }

    public void StoreSpell(Spell spell)
    {
        for (int i = 0; i < spellSlots.Count; i++)
        {
            if (spellSlots[i].currentSpell == null)
            {
                spellSlots[i].SetCurrentSpell(spell);
                SetActiveSlot(i);
                UpdateSlotIcons();
                break;
            }
        }
    }

    public void ShiftSlotsLeft()
    {
        for (int i = 0; i < spellSlots.Count - 1; i++)
        {
            if (spellSlots[i].currentSpell == null &&
                spellSlots[i + 1].currentSpell != null)
            {
                spellSlots[i].SetCurrentSpell(spellSlots[i + 1].currentSpell);
                spellSlots[i].spellChargesLeft = spellSlots[i + 1].spellChargesLeft;
                spellSlots[i].VFXDisplay = spellSlots[i + 1].VFXDisplay;
                spellSlots[i + 1].ClearSlot();
            }
        }

        for (int i = currentActiveSlot; i < spellSlots.Count; i++)
        {
            if (spellSlots[i].currentSpell != null)
            {
                SetActiveSlot(i);
                break;
            }
        }

        if (spellSlots[currentActiveSlot].currentSpell == null)
        {
            for (int i = 0; i < spellSlots.Count; i++)
            {
                if (spellSlots[i].currentSpell != null)
                {
                    SetActiveSlot(i);
                    break;
                }
            }
        }

        UpdateSlotIcons();
    }

    // -------------------------
    //   Pattern recognition
    // -------------------------

    public Spell CheckPatternMatch(List<int> drawnPattern)
    {
        foreach (Spell spell in availableSpells)
        {
            if (IsPatternMatching(spell.pattern, drawnPattern))
                return spell;
        }
        return null;
    }

    /// <summary>
    /// Update right-side UI while drawing.
    /// No match → hide UI.
    /// Match → show matched spell data.
    /// </summary>
    public void UpdateActivePoints(List<int> activePoints)
    {
        HidePatternUI();

        Spell matchedSpell = CheckPatternMatch(activePoints);
        if (matchedSpell != null)
        {
            ShowPatternUI(matchedSpell);
        }
    }

    // Pure pattern comparison, UI is not touched
    private bool IsPatternMatching(List<int> spellPattern, List<int> drawnPattern)
    {
        if (spellPattern == null || drawnPattern == null) return false;
        if (spellPattern.Count != drawnPattern.Count) return false;

        for (int i = 0; i < spellPattern.Count; i++)
        {
            if (spellPattern[i] != drawnPattern[i]) return false;
        }
        return true;
    }

    // -------------------------
    //   Right-side pattern UI
    // -------------------------

    private void HidePatternUI()
    {
        if (patternSlotIcon != null)
        {
            patternSlotIcon.gameObject.SetActive(false);
            patternSlotIcon.sprite = null;
        }

        if (spellName != null)
        {
            spellName.gameObject.SetActive(false);
            spellName.text = string.Empty;
        }

        if (spellCost != null)
        {
            spellCost.gameObject.SetActive(false);
            spellCost.text = string.Empty;
        }

        if (spellCostTitle != null)
        {
            spellCostTitle.gameObject.SetActive(false);
            spellCostTitle.text = string.Empty;
        }
    }

    private void ShowPatternUI(Spell spell)
    {
        if (patternSlotIcon != null)
        {
            patternSlotIcon.sprite = spell != null ? spell.UIIcon : null;
            patternSlotIcon.gameObject.SetActive(spell != null && spell.UIIcon != null);
        }

        if (spellName != null)
        {
            if (spell != null)
            {
                spellName.text = spell.spellName;
                spellName.gameObject.SetActive(true);
            }
            else
            {
                spellName.text = string.Empty;
                spellName.gameObject.SetActive(false);
            }
        }

        if (spellCost != null)
        {
            if (spell != null)
            {
                spellCost.text = spell.manaCost.ToString();
                spellCost.gameObject.SetActive(true);
            }
            else
            {
                spellCost.text = string.Empty;
                spellCost.gameObject.SetActive(false);
            }
        }

        if (spellCostTitle != null)
        {
            if (spell != null)
            {
                spellCostTitle.text = SpellCostTitleText;
                spellCostTitle.gameObject.SetActive(true);
            }
            else
            {
                spellCostTitle.text = string.Empty;
                spellCostTitle.gameObject.SetActive(false);
            }
        }
    }

    // -------------------------
    //   Helper methods for slots
    // -------------------------

    public int AvailableSlots()
    {
        int amount = 0;
        foreach (SpellSlot spellSlot in spellSlots)
        {
            if (spellSlot.currentSpell == null) amount++;
        }
        return amount;
    }

    // -------------------------
    //   VFX + slot icons
    // -------------------------

    public void UpdateSpellVFX()
    {
        if (activeSpellVFX != null)
        {
            Destroy(activeSpellVFX);
            activeSpellVFX = null;
        }

        var slot = (currentActiveSlot >= 0 && currentActiveSlot < spellSlots.Count)
            ? spellSlots[currentActiveSlot]
            : null;

        if (slot != null && slot.currentSpell != null && slot.currentSpell.VFXDisplay != null)
        {
            activeSpellVFX = Instantiate(
                slot.currentSpell.VFXDisplay,
                slot.shootPoint != null ? slot.shootPoint.position : Vector3.zero,
                Quaternion.identity
            );

            if (slot.shootPoint != null)
            {
                activeSpellVFX.transform.SetParent(slot.shootPoint);
                activeSpellVFX.transform.localPosition = Vector3.zero;
                activeSpellVFX.transform.localRotation = Quaternion.identity;
            }
        }
    }

    public void UpdateSlotIcons()
    {
        if (!CanCast)
        {
            if (slotsParent != null)
                slotsParent.gameObject.SetActive(false);
            return;
        }

        if (slotsParent != null)
            slotsParent.gameObject.SetActive(true);

        for (int i = 0; i < spellSlots.Count; i++)
        {
            var slot = spellSlots[i];
            if (slot.currentSpell == null) slot.SetSlotToEmpty();
            else slot.SetSlotToInactive();
        }

        if (currentActiveSlot >= 0 &&
            currentActiveSlot < spellSlots.Count &&
            spellSlots[currentActiveSlot]?.currentSpell != null)
        {
            spellSlots[currentActiveSlot].SetSlotToActive();
        }
    }
}
