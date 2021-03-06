﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq.Expressions;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Disposables;

namespace ReactiveXaml
{
    /// <summary>
    /// 
    /// </summary>
    [DataContract]
    public class ReactiveObject : IReactiveNotifyPropertyChanged
    {
        /// <summary>
        /// 
        /// </summary>
        [field:IgnoreDataMember]
        public event PropertyChangingEventHandler PropertyChanging;

        /// <summary>
        /// 
        /// </summary>
        [field:IgnoreDataMember]
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 
        /// </summary>
        [IgnoreDataMember]
        public IObservable<IObservedChange<object, object>> Changing {
            get { return changingSubject; }
        }

        /// <summary>
        /// 
        /// </summary>
        [IgnoreDataMember]
        public IObservable<IObservedChange<object, object>> Changed {
            get {
#if DEBUG
                this.Log().DebugFormat("Changed Subject 0x{0:X}", changedSubject.GetHashCode());
#endif
                return changedSubject;
            }
        }

        [IgnoreDataMember]
        protected Lazy<PropertyInfo[]> allPublicProperties;

        [IgnoreDataMember] 
        readonly Subject<IObservedChange<object, object>> changingSubject = new Subject<IObservedChange<object, object>>();

        [IgnoreDataMember]
        readonly Subject<IObservedChange<object, object>> changedSubject = new Subject<IObservedChange<object, object>>();

        [IgnoreDataMember]
        long changeNotificationsSuppressed = 0;
        
        // Constructor
        protected ReactiveObject()
        {
            setupRxObj();
        }

        [OnDeserialized]
        void setupRxObj(StreamingContext sc) { setupRxObj(); }

        void setupRxObj()
        {
            allPublicProperties = new Lazy<PropertyInfo[]>(() =>
                GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IDisposable SuppressChangeNotifications()
        {
            Interlocked.Increment(ref changeNotificationsSuppressed);
            return Disposable.Create(() =>
                Interlocked.Decrement(ref changeNotificationsSuppressed));
        }

        protected internal void raisePropertyChanging(string propertyName)
        {
            Contract.Requires(propertyName != null);

            verifyPropertyName(propertyName);
            if (!areChangeNotificationsEnabled)
                return;

            var handler = this.PropertyChanging;
            if (handler != null) {
                var e = new PropertyChangingEventArgs(propertyName);
                handler(this, e);
            }

            notifyObservable(new ObservedChange<object, object>() {
                PropertyName = propertyName, Sender = this, Value = null
            }, changingSubject);
        }

        protected internal void raisePropertyChanged(string propertyName)
        {
            Contract.Requires(propertyName != null);

            verifyPropertyName(propertyName);
            this.Log().DebugFormat("{0:X}.{1} changed", this.GetHashCode(), propertyName);

            if (!areChangeNotificationsEnabled) {
                this.Log().DebugFormat("Suppressed change");
                return;
            }

            var handler = this.PropertyChanged;
            if (handler != null) {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }

            notifyObservable(new ObservedChange<object, object>() {
                PropertyName = propertyName, Sender = this, Value = null
            }, changedSubject);
        }

        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        void verifyPropertyName(string propertyName)
        {
            Contract.Requires(propertyName != null);

            // If you raise PropertyChanged and do not specify a property name,
            // all properties on the object are considered to be changed by the binding system.
            if (String.IsNullOrEmpty(propertyName))
                return;

#if !SILVERLIGHT
            // Verify that the property name matches a real,
            // public, instance property on this object.
            if (TypeDescriptor.GetProperties(this)[propertyName] == null) {
                string msg = "Invalid property name: " + propertyName;
                this.Log().Error(msg);
            }
#endif
        }

        protected bool areChangeNotificationsEnabled {
            get { 
#if SILVERLIGHT
                // N.B. On most architectures, machine word aligned reads are 
                // guaranteed to be atomic - sorry WP7, you're out of luck
                return changeNotificationsSuppressed == 0;
#else
                return (Interlocked.Read(ref changeNotificationsSuppressed) == 0); 
#endif
            }
        }

        void notifyObservable<T>(T item, Subject<T> subject)
        {
#if DEBUG
            this.Log().DebugFormat("Firing observable to subject 0x{0:X}", subject.GetHashCode());
#endif
            try {
                subject.OnNext(item);
            } catch (Exception ex) {
                this.Log().Error(ex);
                subject.OnError(ex);
            }
        }
    } 

    public static class ReactiveObjectExpressionMixin
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TObj"></typeparam>
        /// <typeparam name="TRet"></typeparam>
        /// <param name="This"></param>
        /// <param name="property"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        public static TRet RaiseAndSetIfChanged<TObj, TRet>(this TObj This, Expression<Func<TObj, TRet>> property, TRet newValue)
            where TObj : ReactiveObject
        {
            Contract.Requires(This != null);
            Contract.Requires(property != null);

            FieldInfo field;
            string prop_name = RxApp.expressionToPropertyName<TObj, TRet>(property);

            field = RxApp.getFieldInfoForProperty<TObj>(prop_name);

            var field_val = field.GetValue(This);

            if (EqualityComparer<TRet>.Default.Equals((TRet)field_val, (TRet)newValue))
                return newValue;

            This.raisePropertyChanging(prop_name);
            field.SetValue(This, newValue);
            This.raisePropertyChanged(prop_name);

            return newValue;
        }
    }
}

// vim: tw=120 ts=4 sw=4 et :
