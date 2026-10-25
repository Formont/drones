using System;
using UnityEngine;

public class FireController : MonoBehaviour
{
    [SerializeField] private ParticleSystem fireBig;
    [SerializeField] private ParticleSystem fireSmall;
    [SerializeField] private ParticleSystem fireAll;
    [SerializeField] private AudioSource fireSound;
    [SerializeField] private Color dieColor;
    [SerializeField] private GameActionController controller;
    [SerializeField] private float DegryFiringSeconds = 5f;
    private ParticleSystem.MainModule fireBigFX;
    private ParticleSystem.MainModule fireSmallFX;
    private ParticleSystem.MainModule fireAllFX;
    private float fading = 0f;
    public bool isLiquidated {get;private set;} = false;

    private void Awake(){
        if (fireBig != null) fireBigFX = fireBig.main;
        if (fireSmall != null) fireSmallFX = fireSmall.main;
        if (fireAll != null) fireAllFX = fireAll.main;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void LessIntensity()
    {
        if(isLiquidated) return;
        
        TimeAttenuation();
            
        if(fading >= Mathf.Round(DegryFiringSeconds / 4f) && fireSmall != null)
            fireSmall.Stop();
        if (fading >= Mathf.Round(DegryFiringSeconds / 2f) && fireBig != null)
            fireBig.Stop();
        if (fading >= Mathf.Round(DegryFiringSeconds)){
            
            isLiquidated = true;
            if (fireAll != null) fireAll.Stop();
            if (fireSound != null) fireSound.Stop();
            // The VFX sample's old GameActionController is optional in campaign missions.
            if (controller != null) controller.ChangeProgress();
        }
           
    }
    private void TimeAttenuation()
    {
        fading += 1 * Time.deltaTime;
    }
}
