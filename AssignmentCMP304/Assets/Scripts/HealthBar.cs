using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Gradient gradient = null;
    [SerializeField] private Image fill = null;
    private Slider slider = null;

    private void Awake()
    {
        // Get the slider component
        slider = GetComponent<Slider>();
        if (slider == null)
        {
            Debug.Log("Slider missing on HealthBar script, Object: " + this.gameObject);
        }
    }

    public void SetHealth(int health)
    {
        slider.value = health;

        fill.color = gradient.Evaluate(slider.normalizedValue);
    }

    public void SetMaxHealth(int maxHealth)
    {
        slider.maxValue = maxHealth;
        slider.value = maxHealth;

        fill.color = gradient.Evaluate(1f);
    }
}
