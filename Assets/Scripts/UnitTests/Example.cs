using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections.Generic;
using System;

public class Example
{
    // A Test behaves as an ordinary method
    [Test]
    public void ExampleSimplePasses()
    {
        // Use the Assert class to test conditions
        // ğŒ®‚ªtrue‚¾‚Á‚½‚ç¬Œ÷
        Assert.That(1 < 10);
    }

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator ExampleWithEnumeratorPasses()
    {
        // Use the Assert class to test conditions.
        // Use yield to skip a frame.
        yield return null;
    }
}
