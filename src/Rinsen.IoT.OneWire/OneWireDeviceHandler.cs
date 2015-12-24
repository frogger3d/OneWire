﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Rinsen.IoT.OneWire
{
    public class OneWireDeviceHandler : IDisposable
    {
        private DS2482_100 _ds2482_100;
        private IList<IOneWireDevice> _oneWireDevices;
        private Dictionary<byte, Type> _oneWireDeviceTypes;
        
        /// <summary>
        /// One wire device handler via DS2482-100
        /// </summary>
        /// <param name="ad0">AD0 addess bit</param>
        /// <param name="ad1">AD1 addess bit</param>
        /// <exception cref="Rinsen.IoT.OneWire.DS2482100DeviceNotFoundException">Thrown if no DS2482-100 device is detected</exception>
        public OneWireDeviceHandler(bool ad0 = true, bool ad1 = true)
        {
            byte address = 0x18;
            if (ad0)
            {
                address |= 1 << 0;
            }
            if (ad1)
            {
                address |= 1 << 1;
            }
            
            _ds2482_100 = new DS2482_100(new I2cDeviceLocator().GetI2cDevice(address).Result);

            try
            {
                _ds2482_100.OneWireReset();
            }
            catch (Exception e)
            {
                throw new DS2482100DeviceNotFoundException("No DS2482-100 detected, check that AD0 and AD1 is correct in ctor and that the physical connection to the DS2482-100 one wire bridge is correct.", e);
            }

            _oneWireDeviceTypes = new Dictionary<byte, Type>();

            AddDeviceType<DS18S20>(0x10);
            AddDeviceType<DS18B20>(0x28);
        }

        private IList<IOneWireDevice> GetConnectedOneWireDevices()
        {
            var _oneWireDevices = new List<IOneWireDevice>();
            var first = true;
            var deviceDetected = _ds2482_100.OneWireReset();

            if (deviceDetected)
            {
                var result = true;
                do
                {
                    if (first)
                    {
                        first = false;
                        result = _ds2482_100.OneWireFirst();
                    }
                    else
                    {
                        result = _ds2482_100.OnoWireNext();
                    }

                    if (result)
                    {
                        AddOneWireDevice();
                    }

                } while (result);
            }

            return _oneWireDevices;
        }

        private void AddOneWireDevice()
        {
            if (_oneWireDeviceTypes.Any(k => k.Key == _ds2482_100.ROM_NO[0]))
            {
                var device = (IOneWireDevice)Activator.CreateInstance(_oneWireDeviceTypes.First(k => k.Key == _ds2482_100.ROM_NO[0]).Value);
                device.DS2482_100 = _ds2482_100;
                device.OneWireAddress = new byte[_ds2482_100.ROM_NO.Length];
                Array.Copy(_ds2482_100.ROM_NO, device.OneWireAddress, _ds2482_100.ROM_NO.Length);
                device.Initialize();
                _oneWireDevices.Add(device);
            }
            else
            {
                _oneWireDevices.Add(new UndefinedOneWireDevice { DS2482_100 = _ds2482_100, OneWireAddress = _ds2482_100.ROM_NO });
            }
        }

        public void AddDeviceType<T>(byte familyCode) where T : IOneWireDevice
        {
            _oneWireDeviceTypes.Add(familyCode, typeof(T));
        }

        public void RefreshDevices()
        {
            this._oneWireDevices = this.GetConnectedOneWireDevices();
        }

        public IEnumerable<IOneWireDevice> GetAllDevices()
        {
            return this._oneWireDevices.AsEnumerable();
        }

        public IEnumerable<T> GetDevices<T>() where T : IOneWireDevice
        {
            return this._oneWireDevices.OfType<T>();
        }

        public void Dispose()
        {
            _ds2482_100.Dispose();
        }
    }
}
