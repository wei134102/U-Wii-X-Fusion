using System;
using System.IO;
using System.Threading;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.GameTransfer.Interfaces;
using U_Wii_X_Fusion.GameTransfer.Models;

namespace U_Wii_X_Fusion.GameTransfer.Protocols
{
    public class WiiTransfer : IGameTransfer
    {
        public event EventHandler<TransferProgress> TransferProgressChanged;
        private bool _isTransferring;
        private bool _cancelRequested;

        public void TransferGame(GameInfo game, string destination)
        {
            if (_isTransferring)
                return;

            _isTransferring = true;
            _cancelRequested = false;

            // 模拟传输过程
            Thread transferThread = new Thread(() =>
            {
                try
                {
                    var progress = new TransferProgress
                    {
                        GameName = game.Title,
                        TotalSize = game.Size,
                        TransferredSize = 0,
                        Percentage = 0,
                        Status = "开始传输",
                        IsComplete = false,
                        IsError = false
                    };

                    OnProgressChanged(progress);

                    // 模拟传输进度
                    long transferred = 0;
                    long chunkSize = game.Size / 100;

                    for (int i = 0; i < 100 && !_cancelRequested; i++)
                    {
                        transferred += chunkSize;
                        progress.TransferredSize = transferred;
                        progress.Percentage = i;
                        progress.Status = $"传输中... {i}%";
                        OnProgressChanged(progress);
                        Thread.Sleep(100); // 模拟传输延迟
                    }

                    if (_cancelRequested)
                    {
                        progress.Status = "传输已取消";
                        progress.IsComplete = false;
                    }
                    else
                    {
                        progress.TransferredSize = game.Size;
                        progress.Percentage = 100;
                        progress.Status = "传输完成";
                        progress.IsComplete = true;
                    }

                    OnProgressChanged(progress);
                }
                catch (Exception ex)
                {
                    var progress = new TransferProgress
                    {
                        GameName = game.Title,
                        Status = "传输失败",
                        IsComplete = false,
                        IsError = true,
                        ErrorMessage = ex.Message
                    };
                    OnProgressChanged(progress);
                }
                finally
                {
                    _isTransferring = false;
                }
            });

            transferThread.Start();
        }

        public void CancelTransfer()
        {
            _cancelRequested = true;
        }

        public bool IsTransferring => _isTransferring;

        public string GetPlatform()
        {
            return "Wii";
        }

        protected virtual void OnProgressChanged(TransferProgress progress)
        {
            TransferProgressChanged?.Invoke(this, progress);
        }
    }
}
