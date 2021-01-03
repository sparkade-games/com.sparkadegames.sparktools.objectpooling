# SparkTools: Object Pooling
A generic object pooling solution for C#, along with a Unity specific application of it.

# Installation
It is recommened to install through the Unity Package Manager.

If you wish to manually install, clone the repository into the `Packages` folder of your project.

# How it works
Object pooling allows you to re-use objects instead of constantly destroying and creating them.

The `GameObjectPool`, `MonoBehaviourPool`, and `ObjectPool` classes can manage this recycling of objects, only creating new objects when needed.

The `ObjectPoolManager` and `GlobalObjectPoolManager` take this one step further and manage object pools, creating new object pools automatically when needed.

# How to Use

## Object Pool Types
- **ObjectPool:** Generic object pool, can be used for any C# objects. In fact every other object pool inherits from this.

- **MonoBehaviourPool:** A Unity specific pool that allows you to push and pull MonoBehaviours based on a prefab containing an instance of the MonoBehaviour, though it still keeps each MonoBehaviour on its own GameObject.

- **GameObjectPool:** A Unity specific pool that allows you to push and pull GameObjects based on a prefab. Every MonoBehaviour that impliments `IPoolable` will recieve notifications.

## Object Pool Managers
Object Pool Managers streamline the use of Unity specific object pools. They are Singletons that you can use to push or pull GameObjects and MonoBehaviours, and pools will be set up and expanded as needed. They come in two flavors:

- **ObjectPoolManager:** Manages object pools that exist for the life of the scene they were created in.

- **GlobalObjectPoolManager:** Manages object pools that exist for the life of the entire game.

Both of these provide options to self manage pools, allowing you to create or destroy them manually as well.

# Example
Let's make a projectile:
```
public class Projectile : MonoBehaviour, IPoolable
{
    public Vector2 direction;

    private float speed;

    public void OnPull()
    {
        speed = 0.0f;
    }

    public void OnPush()
    {
    }

    void Update()
    {
        // move in direction
        // increase speed as we move
    }
}
```
Notice we implimented the `IPoolable` interface, which gives us the `OnPull` and `OnPush` callbacks. We use this to reset the projectile's speed every time it is pulled.

Now, we can use the ObjectPoolManager to spawn our projectiles:
```
public class Gun : MonoBehaviour
{
    public Projectile prefab;

    void Update()
    {
        if (Input.GetButtonDown("Shoot"))
        {
            Projectile proj = ObjectPoolManager.Instance.Pull(prefab);
            proj.direction = transform.right;
        }
    }
}
```
So when our shoot button is pressed projectiles are pulled from the Projectile object pool, or created when needed. Of course you'd also want the to be pushed back into the pool at some point, if they hit a wall for instance.