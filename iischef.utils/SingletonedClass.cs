using System;
using System.Runtime.CompilerServices;

namespace iischef.utils
{
    public abstract class SingletonedClass<TSingletonedClass>
        where TSingletonedClass : class, new()
    {
        private static readonly object Padlock = new object();

        // Almacen del singleton de acceso
        private static TSingletonedClass InstanceSingletoned = null;
        private static bool Initializing = false;

        // creador sincronizado para protegerse de posibles problemas multi-hilo
        // otra prueba para evitar instanciación múltiple 
        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void CreateInstance()
        {
            InstanceSingletoned = new TSingletonedClass();
        }

        protected void ResetInstance()
        {
            InstanceSingletoned = null;
        }

        // Obtiene la instancia actual del singleton
        public static TSingletonedClass Instance
        {
            get
            {
                if (Initializing)
                {
                    // En escenarios concurrentes es posible que inicialicemos en paralelo. Es por ello
                    // que intentaremos esperar un poco antes de lanzar la excepción.

                    int maxwait = 250; // Tiempo entre pausas
                    int numpauses = 3; // Número máximo de esperas o pausas

                    for (int pause = 0; pause < numpauses; pause++)
                    {
                        System.Threading.Thread.Sleep(maxwait);
                        if (!Initializing)
                        { 
                            // Bingo!! la espera ha tenido sus frutos: la instancia se ha creado
                            break;
                        }
                    }

                    if (Initializing)
                    {
                        throw new Exception("CE: Cannot acces singletoned instance while class is being instanced");
                    }
                }

                Exception exGeneratedException = null;
                if (InstanceSingletoned == null)
                {
                    lock (Padlock)
                    {
                        if (InstanceSingletoned == null)
                        {
                            try
                            {
                                Initializing = true;
                                CreateInstance();
                            }
                            catch (Exception ex)
                            {
                                InstanceSingletoned = null;
                                exGeneratedException = ex;
                            }
                            finally
                            {
                                Initializing = false;
                            }
                        }
                    }

                    if (exGeneratedException != null)
                    {
                        throw exGeneratedException;
                    }

                    return InstanceSingletoned;
                }

                return InstanceSingletoned;
            }
        }
    }
}
