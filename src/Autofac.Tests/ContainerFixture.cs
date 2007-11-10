﻿using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Autofac.Component.Registration;
using Autofac.Component.Activation;
using Autofac.Component.Scope;
using Autofac.Builder;

namespace Autofac.Tests
{
    [TestFixture]
    public class ContainerFixture
    {
		[Test]
		public void ResolveOptional()
		{
			var target = new Container();
			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(string) },
				new ProvidedInstanceActivator("Hello")));

			var inst = target.ResolveOptional<string>();

			Assert.AreEqual("Hello", inst);
		}

		[Test]
		public void ResolveOptionalNotPresent()
		{
			var target = new Container();
			var inst = target.ResolveOptional<string>();
			Assert.IsNull(inst);
		}

		[Test]
        public void RegisterInstance()
        {
            var builder = new ContainerBuilder();

            var instance = new object();

            builder.Register(instance);

			var target = builder.Build();

            Assert.AreSame(instance, target.Resolve<object>());
            Assert.IsTrue(target.IsRegistered<object>());
        }

        [Test]
        public void ReplaceInstance()
        {
            var target = new Container();

            var instance1 = new object();
            var instance2 = new object();

			target.RegisterComponent(new ComponentRegistration(
				new[]{typeof(object)},
				new ProvidedInstanceActivator(instance1)));

			target.RegisterComponent(new ComponentRegistration(
				new[]{typeof(object)},
				new ProvidedInstanceActivator(instance2)));

            Assert.AreSame(instance2, target.Resolve<object>());
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SameServiceMultipleTimes()
        {
            var target = new Container();
			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(object), typeof(object) },
				new ProvidedInstanceActivator(new object())));
		}

        [Test]
        public void RegisterComponent()
        {
            var registration = new ComponentRegistration(
                new[] { typeof(object), typeof(string) },
                new ProvidedInstanceActivator("Hello"),
                new ContainerScope());

            var target = new Container();

            target.RegisterComponent(registration);

            Assert.IsTrue(target.IsRegistered<object>());
            Assert.IsTrue(target.IsRegistered<string>());
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RegisterComponentNull()
        {
            var target = new Container();

            target.RegisterComponent(null);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void RegisterComponentNullService()
        {
            var registration = new ComponentRegistration(
                new Type[] { typeof(object), null },
                new ProvidedInstanceActivator(new object()),
                new ContainerScope());

            var target = new Container();

            target.RegisterComponent(registration);
        }

        [Test]
        public void RegisterDelegate()
        {
            object instance = new object();
            var target = new Container();
			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(object) },
				new DelegateActivator((c, p) => instance)));
			Assert.AreSame(instance, target.Resolve<object>());
        }

        [Test]
        public void RegisterType()
        {
            var builder = new ContainerBuilder();
			builder.Register<object>();
			var target = builder.Build();
            object instance = target.Resolve<object>();
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(typeof(object), instance);
        }

        [Test]
        public void ResolveUnregistered()
        {
            try
            {
                var target = new Container();
                target.Resolve<object>();
            }
            catch (ComponentNotRegisteredException se)
            {
                Assert.IsTrue(se.Message.Contains("System.Object"));
                return;
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected a ComponentNotRegisteredException, got {0}.", ex);
                return;
            }

            Assert.Fail("Expected a ComponentNotRegisteredException.");
        }

        [Test]
        public void CircularDependency()
        {
            try
            {
				var builder = new ContainerBuilder();
				builder.Register(c => c.Resolve<object>());

				var target = builder.Build();
                target.Resolve<object>();
            }
            catch (DependencyResolutionException de)
            {
                Assert.IsNull(de.InnerException);
                Assert.IsTrue(de.Message.Contains("System.Object -> System.Object"));
                return;
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected a DependencyResolutionException, got {0}.", ex);
                return;
            }

            Assert.Fail("Expected a DependencyResolutionException.");
        }

        // In the below scenario, B depends on A, CD depends on A and B,
        // and E depends on IC and B.

        #region Scenario Classes

        class A : DisposeTracker { }

        class B : DisposeTracker {
            public A A;

            public B(A a) {
                A = a;
            }
        }
        
        interface IC { }

        class C : DisposeTracker {
            public B B;

            public C(B b) {
                B = b;
            }
        }
        
        interface ID { }

        class CD : DisposeTracker, IC, ID {
            public A A;
            public B B;

            public CD(A a, B b) {
                A = a;
                B = b;
            }
        }

        class E : DisposeTracker {
            public B B;
            public IC C;

            public E(B b, IC c) {
                B = b;
                C = c;
            }
        }

        class F {
            public IList<A> AList;
            public F(IList<A> aList) {
                AList = aList;
            }
        }

        #endregion

        [Test]
        [ExpectedException(typeof(DependencyResolutionException))]
        public void InnerCannotResolveOuterDependencies()
        {
            var outerBuilder = new ContainerBuilder();
            outerBuilder.Register<B>();
            var outer = outerBuilder.Build();

            var innerBuilder = new ContainerBuilder();
            innerBuilder.Register<C>();
            innerBuilder.Register<A>();
            var inner = outer.CreateInnerContainer();
            innerBuilder.Build(inner);

            var unused = inner.Resolve<C>();
        }

        [Test]
        public void OuterInstancesCannotReferenceInner()
        {
            var builder = new ContainerBuilder();
            builder.Register<A>().WithScope(InstanceScope.Container);
            builder.Register<B>().WithScope(InstanceScope.Factory);

            var outer = builder.Build();

            var inner = outer.CreateInnerContainer();

            var outerB = outer.Resolve<B>();
            var innerB = inner.Resolve<B>();
            var outerA = outer.Resolve<A>();
            var innerA = inner.Resolve<A>();

            Assert.AreSame(innerA, innerB.A);
            Assert.AreSame(outerA, outerB.A);
            Assert.AreNotSame(innerA, outerA);
            Assert.AreNotSame(innerB, outerB);
        }

        [Test]
        public void IntegrationTest()
        {
            var builder = new ContainerBuilder();

            builder.Register<A>();
            builder.Register<CD>().As<IC, ID>();
            builder.Register<E>();
			builder.Register(ctr => new B(ctr.Resolve<A>()))
				.WithScope(InstanceScope.Factory);

			var target = builder.Build();

			E e = target.Resolve<E>();
            A a = target.Resolve<A>();
            B b = target.Resolve<B>();
            IC c = target.Resolve<IC>();
            ID d = target.Resolve<ID>();

            Assert.IsInstanceOfType(typeof(CD), c);
            CD cd = (CD)c;

            Assert.AreSame(a, b.A);
            Assert.AreSame(a, cd.A);
            Assert.AreNotSame(b, cd.B);
            Assert.AreSame(c, e.C);
            Assert.AreNotSame(b, e.B);
            Assert.AreNotSame(e.B, cd.B);
        }

        [Test]
        public void DisposeOrder1()
        {
			var target = new Container();

			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(A) },
				new ReflectionActivator(typeof(A))));

			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(B) },
				new ReflectionActivator(typeof(B))));

            A a = target.Resolve<A>();
            B b = target.Resolve<B>();

            Queue<object> disposeOrder = new Queue<object>();

            a.Disposing += (s, e) => disposeOrder.Enqueue(a);
            b.Disposing += (s, e) => disposeOrder.Enqueue(b);

            target.Dispose();

            // B depends on A, therefore B should be disposed first
            
            Assert.AreEqual(2, disposeOrder.Count);
            Assert.AreSame(b, disposeOrder.Dequeue());
            Assert.AreSame(a, disposeOrder.Dequeue());
        }

        // In this version, resolve order is reversed.
        [Test]
        public void DisposeOrder2()
        {
			var target = new Container();

			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(A) },
				new ReflectionActivator(typeof(A))));

			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(B) },
				new ReflectionActivator(typeof(B))));

            B b = target.Resolve<B>();
            A a = target.Resolve<A>();

            Queue<object> disposeOrder = new Queue<object>();

            a.Disposing += (s, e) => disposeOrder.Enqueue(a);
            b.Disposing += (s, e) => disposeOrder.Enqueue(b);

            target.Dispose();

            // B depends on A, therefore B should be disposed first
            
            Assert.AreEqual(2, disposeOrder.Count);
            Assert.AreSame(b, disposeOrder.Dequeue());
            Assert.AreSame(a, disposeOrder.Dequeue());
        }

        [Test]
        public void ResolveDependenciesOfCollection()
        {
			var builder = new ContainerBuilder();

			builder.Register<A>();
			builder.RegisterAsCollection<B>();
			builder.Register<B>();
			builder.Register<B>();

			var target = builder.Build();

            var bList = target.Resolve<IList<B>>();

            Assert.IsNotNull(bList);
            Assert.AreEqual(2, bList.Count);
            Assert.IsNotNull(bList[0].A);
            Assert.IsNotNull(bList[1].A);
            Assert.AreEqual(bList[0].A, bList[1].A);
        }

        [Test]
        public void DependencyOnCollection()
        {
			var builder = new ContainerBuilder();

			builder.RegisterAsCollection<A>();
			builder.Register<A>();
			builder.Register<A>();

			builder.Register<F>();

			var target = builder.Build();

            F instance = target.Resolve<F>();

            Assert.IsNotNull(instance);
            Assert.IsNotNull(instance.AList);
            Assert.AreEqual(2, instance.AList.Count);
        }

		[Test]
		public void ResolveSingletonFromContext()
		{
			var builder = new ContainerBuilder();

			builder.Register<A>();

			var target = builder.Build();

			var context = target.CreateInnerContainer();

			var ctxA = context.Resolve<A>();
			var targetA = target.Resolve<A>();

			Assert.AreSame(ctxA, targetA);
			Assert.IsNotNull(ctxA);

			Assert.IsFalse(ctxA.IsDisposed);

			context.Dispose();

			Assert.IsFalse(ctxA.IsDisposed);

			target.Dispose();

			Assert.IsTrue(ctxA.IsDisposed);
		}

		[Test]
		public void ResolveTransientFromContext()
		{
			var target = new Container();

			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(A) },
				new ReflectionActivator(typeof(A)),
				new FactoryScope()));

			var context = target.CreateInnerContainer();

			var ctxA = context.Resolve<A>();
			var targetA = target.Resolve<A>();

			Assert.IsNotNull(ctxA);
			Assert.IsNotNull(targetA);
			Assert.AreNotSame(ctxA, targetA);

			Assert.IsFalse(targetA.IsDisposed);
			Assert.IsFalse(ctxA.IsDisposed);

			context.Dispose();

			Assert.IsFalse(targetA.IsDisposed);
			Assert.IsTrue(ctxA.IsDisposed);

			target.Dispose();

			Assert.IsTrue(targetA.IsDisposed);
			Assert.IsTrue(ctxA.IsDisposed);
		}

		[Test]
		public void ResolveScopedFromContext()
		{
			var target = new Container();

			target.RegisterComponent(new ComponentRegistration(
				new[] { typeof(A) },
				new ReflectionActivator(typeof(A)),
				new ContainerScope()));

			var context = target.CreateInnerContainer();

			var ctxA = context.Resolve<A>();
			var ctxA2 = context.Resolve<A>();

			Assert.IsNotNull(ctxA);
			Assert.AreSame(ctxA, ctxA2);

			var targetA = target.Resolve<A>();
			var targetA2 = target.Resolve<A>();

			Assert.IsNotNull(targetA);
			Assert.AreSame(targetA, targetA2);
			Assert.AreNotSame(ctxA, targetA);

			Assert.IsFalse(targetA.IsDisposed);
			Assert.IsFalse(ctxA.IsDisposed);

			context.Dispose();

			Assert.IsFalse(targetA.IsDisposed);
			Assert.IsTrue(ctxA.IsDisposed);

			target.Dispose();

			Assert.IsTrue(targetA.IsDisposed);
			Assert.IsTrue(ctxA.IsDisposed);
		}

		[Test]
		public void ActivatingFired()
		{
			var instance = new object();
			var container = new Container();
			var registration = new ComponentRegistration(
							new[] { typeof(object) },
							new ProvidedInstanceActivator(instance));
			container.RegisterComponent(registration);

			bool eventFired = false;

			container.Activating += (sender, e) =>
			{
				Assert.AreSame(container, sender);
				Assert.AreSame(instance, e.Instance);
				Assert.AreSame(container, e.Context);
				Assert.AreSame(registration, e.Component);
				eventFired = true;
			};

			registration.ResolveInstance(container, ActivationParameters.Empty, new Disposer());

			Assert.IsTrue(eventFired);
		}

		[Test]
		public void ActivatedFired()
		{
			var instance = new object();
			var container = new Container();
			var registration = new ComponentRegistration(
							new[] { typeof(object) },
							new ProvidedInstanceActivator(instance));
			container.RegisterComponent(registration);

			bool eventFired = false;

			container.Activated += (sender, e) =>
			{
				Assert.AreSame(container, sender);
				Assert.AreSame(instance, e.Instance);
				Assert.AreSame(registration, e.Component);
				eventFired = true;
			};

			container.Resolve<object>();

			Assert.IsTrue(eventFired);
		}


		[Test]
		public void ActivatingFiredInSubcontext()
		{
			var cb = new ContainerBuilder();
			cb.Register<object>().WithScope(InstanceScope.Factory);
			var container = cb.Build();

			bool eventFired = false;

			var context = container.CreateInnerContainer();

			container.Activating += (sender, e) =>
			{
				Assert.AreSame(container, sender);
				eventFired = true;
			};

			context.Resolve<object>();

			Assert.IsTrue(eventFired);
		}

		class ObjectRegistrationSource : IRegistrationSource
		{
			public bool TryGetRegistration(Type service, out IComponentRegistration registration)
			{
				Assert.AreEqual(typeof(object), service);
				registration = new ComponentRegistration(
					new[] { typeof(object) },
					new ReflectionActivator(typeof(object)));
				return true;
			}
		}

		[Test]
		public void AddRegistrationInServiceNotRegistered()
		{
			var c = new Container();

			Assert.IsFalse(c.IsRegistered<object>());

			c.AddRegistrationSource(new ObjectRegistrationSource());

			Assert.IsTrue(c.IsRegistered<object>());

			var o = c.Resolve<object>();
			Assert.IsNotNull(o);
		}

        [Test]
        public void ResolveByName()
        {
            var r = new ComponentRegistration(
                new Type[] { },
                new ReflectionActivator(typeof(object)));

            Assert.IsNotNull(r.Name);
            Assert.IsNotEmpty(r.Name);

            var c = new Container();
            c.RegisterComponent(r);

            object o;
            
            Assert.IsTrue(c.TryResolve(r.Name, out o));
            Assert.IsNotNull(o);

            Assert.IsFalse(c.IsRegistered<object>());
        }
	}
}