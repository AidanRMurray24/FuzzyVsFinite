﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawHidingSpotNodes : MonoBehaviour
{
	private void OnDrawGizmos()
	{
		foreach (Transform t in transform)
		{
			Gizmos.DrawSphere(t.position, 1);
		}
	}
}
