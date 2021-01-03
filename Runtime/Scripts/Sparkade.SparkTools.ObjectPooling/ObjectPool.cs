﻿namespace Sparkade.SparkTools.ObjectPooling
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A completely generic object pool.
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the pool.</typeparam>
    public class ObjectPool<T> : IPool<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
        /// </summary>
        /// <param name="factory">A method which takes in a pool and returns an object for that pool.</param>
        /// <param name="size">The target size of the pool. The pool will expand beyond this size if needed.</param>
        /// <param name="accessMode">Determines the order in which a pool pulls objects from its store.</param>
        /// <param name="loadingMode">Determines how a pool reaches its size.</param>
        public ObjectPool(
            Func<ObjectPool<T>, T> factory,
            int size = 0,
            PoolAccessMode accessMode = PoolAccessMode.LastIn,
            PoolLoadingMode loadingMode = PoolLoadingMode.Eager)
        {
            if (size < 0)
            {
                throw new ArgumentOutOfRangeException("size", size, "Argument 'size' cannot be negative.");
            }

            this.Factory = factory ?? throw new ArgumentNullException("factory");
            this.Size = size;
            this.AccessMode = accessMode;
            this.LoadingMode = loadingMode;
            this.ItemStore = this.CreateItemStore(this.AccessMode, this.Size);

            if (loadingMode == PoolLoadingMode.Eager)
            {
                this.PreloadItems();
            }
        }

        /// <summary>
        /// Impliments a generic store for objects that can be added to and retrieved from.
        /// </summary>
        protected interface IItemStore
        {
            /// <summary>
            /// Removes an object from the store and returns it.
            /// </summary>
            /// <returns>The object removed form the store.</returns>
            T Fetch();

            /// <summary>
            /// Places an object into the store.
            /// </summary>
            /// <param name="item">The object to be stored.</param>
            void Store(T item);
        }

        /// <inheritdoc/>
        public int Count => this.OwnedItems.Count;

        /// <inheritdoc/>
        public int FreeCount => this.StoredItems.Count;

        /// <inheritdoc/>
        public int InUseCount => this.Count - this.FreeCount;

        /// <inheritdoc/>
        public int Size { get; }

        /// <inheritdoc/>
        public PoolAccessMode AccessMode { get; }

        /// <inheritdoc/>
        public PoolLoadingMode LoadingMode { get; }

        /// <summary>
        /// Gets or sets a callback for when an object is pulled.
        /// </summary>
        public Action<T> OnPull { get; set; }

        /// <summary>
        /// Gets or sets a callback for when an object is pushed.
        /// </summary>
        public Action<T> OnPush { get; set; }

        /// <summary>
        /// Gets or sets a method for creating an object for the pool.
        /// </summary>
        protected Func<ObjectPool<T>, T> Factory { get; set; }

        /// <summary>
        /// Gets or sets the generic item store for the pool.
        /// </summary>
        protected IItemStore ItemStore { get; set; }

        /// <summary>
        /// Gets or sets a collection of all objects owned by the pool.
        /// </summary>
        protected HashSet<T> OwnedItems { get; set; }

        /// <summary>
        /// Gets or sets a collection of all objects currently in the pool.
        /// </summary>
        protected HashSet<T> StoredItems { get; set; }

        /// <inheritdoc/>
        public virtual T Pull()
        {
            T item = this.PullWithoutCallback();
            (item as IPoolable)?.OnPull();
            this.OnPull?.Invoke(item);
            return item;
        }

        /// <inheritdoc/>
        public virtual void Push(T item)
        {
            this.PushWithoutCallback(item);
            (item as IPoolable)?.OnPush();
            this.OnPush?.Invoke(item);
        }

        /// <summary>
        /// Pulls a given object without triggering the IPoolable 'OnPull' callback.
        /// </summary>
        /// <returns>The object pulled.</returns>
        public virtual T PullWithoutCallback()
        {
            T item;
            if (this.FreeCount == 0 || (this.LoadingMode == PoolLoadingMode.Lazy && this.Count < this.Size))
            {
                item = this.CreateItem();
            }
            else
            {
                item = this.FetchItem();
            }

            return item;
        }

        /// <summary>
        /// Pushes a given object without triggering the IPoolable 'OnPush' callback.
        /// </summary>
        /// <param name="item">The object to be pushed.</param>
        public virtual void PushWithoutCallback(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (!this.OwnedItems.Contains(item))
            {
                throw new ArgumentException("Item does not belong to the pool.", "item");
            }

            if (this.StoredItems.Contains(item))
            {
                throw new ArgumentException("Item is already in the pool.", "item");
            }

            this.StoreItem(item);
        }

        /// <summary>
        /// Initializes the item store for the pool.
        /// </summary>
        /// <param name="mode">Access mode of the pool.</param>
        /// <param name="size">Size of the pool.</param>
        /// <returns>The created item store.</returns>
        protected virtual IItemStore CreateItemStore(PoolAccessMode mode, int size)
        {
            this.OwnedItems = new HashSet<T>();
            this.StoredItems = new HashSet<T>();

            return mode switch
            {
                PoolAccessMode.FirstIn => new QueueStore(size),
                PoolAccessMode.LastIn => new StackStore(size),
                _ => null,
            };
        }

        /// <summary>
        /// Creates a new object and places it in the item store.
        /// </summary>
        /// <returns>The object created.</returns>
        protected virtual T CreateItem()
        {
            T item = this.Factory(this);
            this.OwnedItems.Add(item);
            return item;
        }

        /// <summary>
        /// Creates enough objects for the pool to reach its size and places them in the item store.
        /// </summary>
        protected virtual void PreloadItems()
        {
            for (int i = 0; i < this.Size; i += 1)
            {
                this.StoreItem(this.CreateItem());
            }
        }

        /// <summary>
        /// Removes an object from the item store and returns it.
        /// </summary>
        /// <returns>The object removed.</returns>
        protected virtual T FetchItem()
        {
            T item = this.ItemStore.Fetch();
            this.StoredItems.Remove(item);
            return item;
        }

        /// <summary>
        /// Stores an object in the item store.
        /// </summary>
        /// <param name="item">The object to be stored.</param>
        protected virtual void StoreItem(T item)
        {
            this.ItemStore.Store(item);
            this.StoredItems.Add(item);
        }

        /// <summary>
        /// A type of item store that is first in first out.
        /// </summary>
        protected class QueueStore : Queue<T>, IItemStore
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="QueueStore"/> class.
            /// </summary>
            /// <param name="size">Initial size of the queue.</param>
            public QueueStore(int size = 0)
                : base(size)
            {
            }

            /// <inheritdoc/>
            public T Fetch()
            {
                return this.Dequeue();
            }

            /// <inheritdoc/>
            public void Store(T item)
            {
                this.Enqueue(item);
            }
        }

        /// <summary>
        /// A type of item store that is last in first out.
        /// </summary>
        protected class StackStore : Stack<T>, IItemStore
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="StackStore"/> class.
            /// </summary>
            /// <param name="size">Initial size of the stack.</param>
            public StackStore(int size = 0)
                : base(size)
            {
            }

            /// <inheritdoc/>
            public T Fetch()
            {
                return this.Pop();
            }

            /// <inheritdoc/>
            public void Store(T item)
            {
                this.Push(item);
            }
        }
    }
}