using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class cubemove : MonoBehaviour
{
    public float speed = 2f;
    // Start is called before the first frame update
    void Start()
    {
        transform.position=Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.right * speed * Time.deltaTime);
    }
}
