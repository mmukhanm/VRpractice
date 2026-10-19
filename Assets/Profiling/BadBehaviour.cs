using System.Collections.Generic;
using UnityEngine;

public class BadBehaviour : MonoBehaviour
{
    void Update()
    {
        GameObject player = GameObject.Find("XR Origin (XR Rig)");
        float distance = Vector3.Distance(transform.position, player.transform.position);
        List<Renderer> list = new List<Renderer>(GetComponentsInChildren<Renderer>());
        string info = "Distance: " + distance.ToString() + " renderers: " + list.Count;
        if (distance < 3f) GetComponent<Renderer>().material.color = Color.red;
    }
}
