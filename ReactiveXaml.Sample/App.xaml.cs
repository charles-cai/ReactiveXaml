﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.ComponentModel.DataAnnotations;
using ReactiveXaml;
using System.Concurrency;

namespace ReactiveXamlSample
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IEnableLogger
    {

        /* COOLSTUFF: Using MEF
         *
         * Here, we're using the Managed Extensibility Framework to pull in
         * dependencies in a loosely-coupled fashion. This is a useful way to
         * decouple objects from one another, as well as lays the framework to
         * more easily add plugin support to your application in the future if
         * it makes sense. It also allows you to replace implementations with
         * mocks in your tests more easily.
         *
         * IMPORTANT: Your Main Window must have an Export attribute which
         * exports an AppView contract (MainWindow.xaml.cs does this by
         * default), and it has to be the *only* AppView export (i.e. you have
         * to remove the Export attribute from MainWindow)
         */

        [Import("AppView")]
        Window theWindow { get; set; }

        void compose()
        {
            var catalog = new AggregateCatalog();
            var thisAssembly = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            catalog.Catalogs.Add(thisAssembly);            

            try {
                var container = new CompositionContainer(catalog);
                container.ComposeParts(this);
            } catch(Exception ex) {
                /* NB: If you're blowing up here, you probably deleted the
                 * sample files without reading the notes. Instead of doing
                 * that, you should keep them as a reference and just move the
                 * Export attribute to your own main window */
                this.Log().Fatal("Failed composition - check your imports and their constructors!", ex);
                throw;
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            this.Log().Debug("Hello world!");

            // Set up our crash handler ASAP
            AppDomain.CurrentDomain.UnhandledException += (o, ex) => {
                this.Log().Fatal("Unhandled Exception - aieee!", ex.ExceptionObject as Exception);
            };

            /* COOLSTUFF: Enabling Debug Mode
             * 
             * Calling this function while in Debug mode enables log statements 
             * to the VS Debug window, as well as enabling several other debug
             * features, such as crashing the application when the Dispatcher queue
             * has been stalled (i.e. your app would've displayed a black screen
             * or hung. This is usually the result of doing a long-running task
             * or a blocking disk/network call on the RxApp.DeferredScheduler when
             * it should've probably be run on the RxApp.TaskpoolScheduler (i.e. not
             * on the UI thread). */

#if DEBUG
            RxApp.EnableDebugMode();
#endif

            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            compose();
            this.MainWindow = theWindow;
            theWindow.Show();
        }
    }

    [Export(typeof(AppViewModel))]
    public class AppViewModel : ReactiveValidatedObject
    {
        ObservableCollection<PersonEntry> _People = new ObservableCollection<PersonEntry>();
        public ObservableCollection<PersonEntry> People { get { return _People; } }

        public AppViewModel() 
        {
            setupCommands();
        }

        PersonEntry _SelectedPerson;
        public PersonEntry SelectedPerson {
            get { return _SelectedPerson; }
            set { _SelectedPerson = this.RaiseAndSetIfChanged(x => x.SelectedPerson, value); }
        }


        /*
         * Commands
         */

        /* COOLSTUFF: Exporting Commands via MEF
         *
         * Commands are a good way for different components in your application
         * (either separate Windows, or UserControls / Templates within the same
         * Window) to communicate with each other without having to explicitly
         * bind them together in the code, or requiring all of your child
         * UserControls to have a reference to the MainWindow control.
         *
         * Instead, the Commanding framework combined with MEF allows us to say,
         * "Please give me the AddPerson command, I don't care who implements
         * it". Here, we export our commands that might be used globally, though
         * in this sample, they aren't. Exporting Commands from the host
         * application are also a good way for add-ins to be able to communicate
         * with the host application.
         */

        [Export("AddPerson", typeof(IReactiveCommand))]
        public ReactiveCommand AddPerson { get; protected set; }

        [Export("RemovePerson", typeof(IReactiveCommand))]
        public ReactiveCommand RemovePerson { get; protected set; }


        /* COOLSTUFF: Setting up Commands
         *
         * This function must be implemented in every ReactiveObservableObject -
         * this is the place where you can actually implement the commands you
         * declare (or more likely, call functions that implement these
         * commands if they are complex).
         *
         * One thing that is important that M-V-VM helps to enforce, is that
         * commands should be acting on the Model and ViewModel data, *not*
         * changing control properties or fiddling with the Window. Your
         * commands act directly on the data, and it is the Bindings' job to
         * reflect that change in the View. When you are disciplined in this
         * fashion, it makes testing your code much easier, since your code
         * won't depend on being inside a WPF Window / Button / whatever. */

        [Import(typeof(IPromptForModelDialog<PersonEntry>))]
        IPromptForModelDialog<PersonEntry> addPersonDialog { get; set; }

        protected void setupCommands()
        {
            AddPerson = ReactiveCommand.Create(null, item => {
                var to_add = (item as PersonEntry) ?? addPersonDialog.Prompt(this, null);

                if (to_add == null)
                    return;
                if (!to_add.IsValid())
                    return;

                this.Log().DebugFormat("Adding '{0}'", to_add.Name);
                People.Add(to_add);
            });

            RemovePerson = ReactiveCommand.Create(param => (param ?? SelectedPerson) != null && People.Count > 0, item => {
                var to_remove = (PersonEntry)item ?? SelectedPerson;
                this.Log().DebugFormat("Removing '{0}'", to_remove.Name);
                People.Remove(to_remove);
            });
        }
    }

    public class PersonEntry : ReactiveValidatedObject
    {
        /* COOLSTUFF: Declaring a Change Notify Property
         *
         * In order for WPF to be able to bind to objects, it has to know when
         * objects change (if it never changes, a regular .NET property will
         * do). ReactiveObservableObject has a lot of stuff that will help is
         * out with this, but at the end of the day, you need to just copy-paste
         * this block of code for every property that changes.
         *
         * ReactiveObservableObject's IObservable implementation will provide a
         * value whenever any property changes. You can also use
         * ObservableForProperty to get an IObservable for any specific
         * property.
         *
         * Yeah, I really wish C# had macros too. Anders, are you listening??
         */
        ImageSource _Image;
        public ImageSource Image {
            get { return _Image; }
            set { _Image = this.RaiseAndSetIfChanged(x => x.Image, value); }
        }

        /* COOLSTUFF: Data Validation
         *
         * In addition to providing change notification support,
         * ReactiveObservableObject also provides validation support via Data
         * Annotations. For every property you want to limit, you can add
         * special attributes from the System.ComponentModel.DataAnnotations
         * namespace (or derive from ValidationAttribute and create your own)
         *
         * To let WPF know about the validation, we implement IDataErrorInfo,
         * which WPF will use to figure out whether a property is valid. The
         * IsValid function will tell us whether all of the properties are
         * valid.
         */

        string _Name;
        [Required]
        [StringLength(35, MinimumLength = 3, ErrorMessage = "Names have to be between 3 and 35 letters long")]
        public string Name {
            get { return _Name; }
            set { _Name = this.RaiseAndSetIfChanged(x => x.Name, value); }
        }

        string _PhoneNumber;
        [Required]
        [RegularExpression(@"\d\d\d\.\d\d\d\.\d\d\d\d", ErrorMessage="Enter a Phone Number i.e. 555.555.1234")]
        public string PhoneNumber {
            get { return _PhoneNumber; }
            set { _PhoneNumber = this.RaiseAndSetIfChanged(x => x.PhoneNumber, value); }
        }

        int _AwesomenessFactor;
        [ValidatesViaMethod(ErrorMessage="Awesomeness must be Even!")]
        public int AwesomenessFactor {
            get { return _AwesomenessFactor; }
            set { _AwesomenessFactor = this.RaiseAndSetIfChanged(x => x.AwesomenessFactor, value); }
        }

        /* COOLSTUFF: ValidatesViaMethod
         *
         * By default, ValidatesViaMethod will invoke a method called
         * Is<PropertyName>Valid - you can also set the name of the method
         * explicitly in the attribute
         */

        public bool IsAwesomenessFactorValid(int value)
        {
            return value % 2 == 0;
        }
    }

    public interface IPromptForModelDialog<T>
    {
        T Prompt(object sender, object parameter);
    }
}
