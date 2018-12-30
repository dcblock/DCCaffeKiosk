﻿using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// for HID
using SharpLib.Win32;

namespace DCafeKiosk
{
    public partial class FormRFRead : Form, IPage
    {
        /// <summary>
        /// 인터페이스
        /// </summary>
        #region 'IPage'
        public event EventHandler<EventArgs> PageSuccess;
        public event EventHandler<EventArgs> PageCancle;

        public void OnPageSuccess()
        {
            if (PageSuccess != null)
                PageSuccess(this, EventArgs.Empty);
        }

        public void OnPageCancle()
        {
            if (PageCancle != null)
                PageCancle(this, EventArgs.Empty);
        }

        public void ResetForm()
        {
            apiResult = false;
            XApiResponse = null;

            strRfid.Clear();
            StartEventCapture();
        }
        #endregion
        
        /// <summary>
        /// 프로퍼티
        /// </summary>        
        #region 'properties'
        /// <summary>
        /// HID에서 읽은 RFID 값 저장
        /// </summary>
        private StringBuilder strRfid = new StringBuilder();
        public String XstrHashedRFid { get; set; }

        /// <summary>
        /// RFID 인증 결과
        /// </summary>
        private bool apiResult = false;
        public DTOGetPurchaseIdResponse XApiResponse { get; set; }
        #endregion

        public FormRFRead()
        {            
            InitializeComponent();
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        /// <summary>
        /// RestAPI 호출하여 RFID 확인
        /// </summary>
        private void RestAPI_CheckRFID()
        {
            System.Threading.Thread.Sleep(2000);

            HashLib.IHash hash = HashLib.HashFactory.Crypto.CreateSHA256();
            HashLib.HashResult digest = hash.ComputeString(strRfid.ToString(), Encoding.ASCII);
            {
                XstrHashedRFid = digest.ToString().Replace("-", "");
            }
            XApiResponse = APIController.API_PostPurchaseId(XstrHashedRFid);

            if (XApiResponse != null && XApiResponse.code == 200)
                apiResult = true;
            else
                apiResult = false;
        }

        /// <summary>
        /// 취소 버튼 클릭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancle_Click(object sender, EventArgs e)
        {
            OnPageCancle();
        }

        /// <summary>
        /// 4Test
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTestRFStart_Click(object sender, EventArgs e)
        {
            strRfid.Clear();
            StartEventCapture();
        }

        /// <summary>
        /// 4Test
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTestRFStop_Click(object sender, EventArgs e)
        {
            strRfid.Clear();
            EndEventCapture();
        }

        #region 'HID MONITORING'
        /// <summary>
        /// RF ID 정보 추출 완료되면 호출됨
        /// </summary>
        /// <param name="str"></param>
        private void CompleatedRFRead(string str)
        {
            EndEventCapture();

            // 데이터 로딩 다이얼로그
            using (FormLoadingDialog form = new FormLoadingDialog(RestAPI_CheckRFID))
            {
                form.TopLevel = true;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ShowDialog();
            }

            if (!apiResult) // 인증 실패
            {
                DCafeKiosk.FormMessageBox dlg = new DCafeKiosk.FormMessageBox();
                dlg.StartPosition = FormStartPosition.CenterParent;
                DialogResult dlgResult = dlg.ShowDialog("사용자 카드를 조회할 수 없습니다.", "인증 실패", CustomMessageBoxButtons.RetryCancel);

                if (dlgResult == DialogResult.Retry)
                {
                    ResetForm(); // 재시도
                }
                else if(dlgResult == DialogResult.Cancel)
                {
                    OnPageCancle(); // 처음 페이지 이동
                }
            }
            else // 인증 성공
            {
                OnPageSuccess();
            }
        }

        /// <summary>
        /// RAWINPUT 이벤트 수신 시작
        /// </summary>
        private void StartEventCapture()
        {            
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];

            // https://docs.microsoft.com/en-us/windows/desktop/api/winuser/ns-winuser-tagrawinputdevice
            // 이 옵션을 설정하면 발신자가 포 그라운드에 있지 않아도 발신자가 입력을받을 수 있습니다. 
            // 참고 hwndTarget을 지정해야 합니다.

            //-------------------------------------------------------
            // RIM_INPUT = FOREGROUND에서만 WM_INPUT 이벤트 수신
            // RIDEV_INPUTSINK = BACKGROUND에서도 WM_INPUT 이벤트 수신
            //-------------------------------------------------------
            RawInputDeviceFlags flags = RawInputDeviceFlags.RIDEV_INPUTSINK;
            {
                rid[0].usUsagePage = (ushort)SharpLib.Hid.UsagePage.GenericDesktopControls;
                rid[0].usUsage = (ushort)SharpLib.Hid.UsageCollection.GenericDesktop.Keyboard;                
                rid[0].dwFlags = flags;
                rid[0].hwndTarget = this.Handle;
            }

            uint size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(RAWINPUTDEVICE));
            if (Function.RegisterRawInputDevices(rid, 1, size) == false)
            {
                Console.WriteLine("ResigterRawInputDevices Error: {0}", Marshal.GetLastWin32Error());
            }
            else
            {
                Console.WriteLine("Registration of device was successful");
            }
        }

        /// <summary>
        /// RAWINPUT 이벤트 수신 중지
        /// </summary>
        private void EndEventCapture()
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];

            RawInputDeviceFlags flags = RawInputDeviceFlags.RIDEV_REMOVE;
            {
                rid[0].usUsagePage = (ushort)SharpLib.Hid.UsagePage.GenericDesktopControls;
                rid[0].usUsage = (ushort)SharpLib.Hid.UsageCollection.GenericDesktop.Keyboard;
                rid[0].dwFlags = flags;             // here!
                rid[0].hwndTarget = IntPtr.Zero;    // here!
            }

            //De-register
            uint size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(RAWINPUTDEVICE));
            if (Function.RegisterRawInputDevices(rid, 1, size) == false)
            {
                Console.WriteLine("De-ResigterRawInputDevices Error: {0}", Marshal.GetLastWin32Error());
            }
            else
            {
                Console.WriteLine("De-Registration of device was successful");
            }
        }

        /// <summary>
        /// HID RAWINPUT 이벤트 핸들러 (WM_INPUT)
        /// </summary>
        /// <param name="message"></param>
        protected override void WndProc(ref Message message)
        {
            switch (message.Msg)
            {
                //------------------------------------------------------------
                // HID RAW INPUT READ

                case Const.WM_INPUT:
                    {
                        IntPtr rawInputBuffer = IntPtr.Zero;
                        RAWINPUT iRawInput = new RAWINPUT();

                        if (!GetRawInputData(message.LParam, ref iRawInput, ref rawInputBuffer))
                        {
                            Console.WriteLine("GetRawInputData failed!");
                            break;
                        }
                        else
                        {
                            //The key is down.
                            if (iRawInput.header.dwType == RawInputDeviceType.RIM_TYPEKEYBOARD
                                && iRawInput.keyboard.Flags == RawInputKeyFlags.RI_KEY_MAKE)
                            {
                                // VKey 정보 처리 하기
                                //----------------------------------------
                                ProcessRawInput(iRawInput.keyboard.VKey);
                                //----------------------------------------

                                Console.WriteLine("RAWINPUT: Header dwSize: 0x{0:X}\r\n",
                                    iRawInput.header.dwSize
                                    );

                                Console.WriteLine("RAWINPUT: VKey: 0x{0:X}\r\n",
                                    iRawInput.keyboard.VKey
                                    );
                            }
                        }
                    }

                    //------------------------------------------------------------

                    //Log that message
                    Console.WriteLine("WM_INPUT: " + message.ToString() + "\r\n");

                    //Returning zero means we processed that message.
                    message.Result = new IntPtr(0);

                    break;
            }
            //Is that needed? Check the docs.
            base.WndProc(ref message);
        }

        /// <summary>
        /// VKEY를 문자열로 변환
        /// </summary>
        /// <param name="virtualKeyCode"></param>
        /// <param name="scanCode"></param>
        /// <param name="keyboardState"></param>
        /// <param name="receivingBuffer"></param>
        /// <param name="bufferSize"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicode(
            uint virtualKeyCode,
            uint scanCode,
            byte[] keyboardState,
            StringBuilder receivingBuffer,
            int bufferSize,
            uint flags
        );

        /// <summary>
        /// WM_INPUT으로 들어오는 RFID의 VKey Code를 문자열 변환 처리
        /// VK_ENTER가 나올때 까지 문자열을 만든다.
        /// </summary>
        /// <param name="VKey"></param>
        public void ProcessRawInput(ushort VKey)
        {
            if (VKey == 0x0D) //VK_ENTER
            {
                // RFCard ID 값 읽기 완료
                //------------------------------------
                CompleatedRFRead(strRfid.ToString());
                //------------------------------------
                strRfid.Clear();
            }
            else
            {
                var buf = new StringBuilder(256);
                var keyboardState = new byte[256];
                ToUnicode((uint)VKey, 0, keyboardState, buf, 256, 0);
                strRfid.Append(buf.ToString());
            }
        }

        /// <summary>
        /// RAWINPUT으로 부터 데이터를 읽는다.
        /// </summary>
        /// <param name="aRawInputHandle"></param>
        /// <param name="aRawInput"></param>
        /// <param name="rawInputBuffer">Caller must free up memory on the pointer using Marshal.FreeHGlobal</param>
        /// <returns></returns>
        public static bool GetRawInputData(IntPtr aRawInputHandle, ref RAWINPUT aRawInput, ref IntPtr rawInputBuffer)
        {
            bool success = true;
            rawInputBuffer = IntPtr.Zero;

            try
            {
                uint dwSize = 0;
                uint sizeOfHeader = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));

                //Get the size of our raw input data.
                uint ret = Function.GetRawInputData(aRawInputHandle, Const.RID_INPUT, IntPtr.Zero, ref dwSize, sizeOfHeader);

                //Allocate a large enough buffer
                rawInputBuffer = Marshal.AllocHGlobal((int)dwSize);

                //Now read our RAWINPUT data
                if (Function.GetRawInputData(aRawInputHandle, Const.RID_INPUT, rawInputBuffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) != dwSize)
                {
                    return false;
                }

                //Cast our buffer
                aRawInput = (RAWINPUT)Marshal.PtrToStructure(rawInputBuffer, typeof(RAWINPUT));
            }
            catch
            {
                Console.WriteLine("GetRawInputData failed!");
                success = false;
            }

            return success;
        }
        #endregion 'HID MONITORING'
    }
}
