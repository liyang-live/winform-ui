namespace Tlw.ZPG.Domain.Models.Trading
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using Tlw.ZPG.Domain.Models.Admin;
    using Tlw.ZPG.Domain.Models.ApplyAccount;
    using Tlw.ZPG.Infrastructure;
    using Tlw.ZPG.Infrastructure.Utils;

    public partial class Trade : EntityBase
    {
        public Trade()
        {
            this.TradeMessages = new HashSet<TradeMessage>();
            this.TradeHangLogs = new HashSet<TradeHangLog>();
            this.Accounts = new HashSet<Account>();
            this.TradeDetails = new HashSet<TradeDetail>();
        }

        #region ����
        public int LandId { get; set; }
        public int AfficheId { get; set; }
        public System.DateTime SignBeginTime { get; set; }
        public System.DateTime SignEndTime { get; set; }
        public System.DateTime TradeBeginTime { get; set; }
        public System.DateTime TradeEndTime { get; set; }
        public decimal StartPrice { get; set; }
        internal string ReservePrice { get; set; }
        public decimal HangPriceIncrease { get; set; }
        public decimal AuctionPriceIncrease { get; set; }
        public decimal CurrentPrice { get; internal set; }
        public TradeStatus Status { get; internal set; }
        public TradeStatus? PrevStatus { get; internal set; }
        public DateTime StatusTime { get; internal set; }
        public System.DateTime? DealTime { get; set; }
        public int DealAccountId { get; set; }
        public int CreatorId { get; set; }
        public decimal DealPrice { get; set; }
        public TradeStage Stage { get; internal set; }
        [Timestamp]
        internal byte[] RowVersion { get; set; }

        public virtual Affiche Affiche { get; set; }
        public virtual Land Land { get; set; }
        public virtual User Creator { get; set; }
        public virtual Account DealAccount { get; set; }
        public virtual ICollection<TradeMessage> TradeMessages { get; internal set; }
        public virtual ICollection<TradeHangLog> TradeHangLogs { get; internal set; }
        public virtual ICollection<TradeBidLog> TradeBidLogs { get; internal set; }
        public virtual ICollection<Account> Accounts { get; internal set; }
        public virtual ICollection<TradeDetail> TradeDetails { get; internal set; }
        public virtual TradeResultConfirm TradeResultConfirm { get; set; } 
        #endregion

        /// <summary>
        /// ��ȡ˳��ʱ��
        /// </summary>
        /// <returns></returns>
        public DateTime GetPostponeTime()
        {
            return this.TradeEndTime + (DateTime.Now - this.StatusTime);
        }

        /// <summary>
        /// �ָ����ⶳ
        /// </summary>
        public void Recover(int userId, DateTime tradeEndTime)
        {
            if (this.Status != TradeStatus.Normal)
            {
                if (this.Status == TradeStatus.Froze)
                {
                    if (userId != this.CreatorId) throw new TradeRecoverException("�㲻�Ǹ��ڵش����ߣ��޷��ⶳ����");
                    var canRecoverHours = Application.GetDictionaryValue("HowManyHoursCanRecoverAfterFrozed", 1);
                    var resumedHours = Application.GetDictionaryValue("ShouldBeResumedAfterHoursLater", 24);
                    if ((DateTime.Now - this.StatusTime).TotalHours > canRecoverHours)
                        throw new TradeRecoverException(string.Format("���ᳬ��{0}��Сʱ��������{1}Сʱ��ⶳ", canRecoverHours, resumedHours));
                    if(GetPostponeTime() < tradeEndTime)
                    {
                        throw new TradeRecoverException("˳��ʱ�䲻��С�ڶ���ʱ��");
                    }
                    SetStatus(TradeStatus.Normal);
                    this.TradeEndTime = tradeEndTime;
                    //����������׶Σ��ⶳ����Ҫ�ص��ȴ��׶�
                    if (this.Stage == TradeStage.Auction)
                    {
                        this.Stage = TradeStage.Ready;
                    }
                }
                else
                {
                    Throw(TradeStatus.Normal);
                }
            }
        }

        /// <summary>
        /// ����
        /// </summary>
        public void Froze(int userId, string innerExplain, string outExplain)
        {
            if (this.Status != TradeStatus.Froze)
            {
                if (this.Status == TradeStatus.Normal)
                {
                    if (userId != this.CreatorId) throw new TradeFrozeException("�㲻�Ǹ��ڵش����ߣ��޷����ύ��");
                    var minutes = Application.GetDictionaryValue("CanFrozeOrTerminateBeforeTradeEndTime", 60);
                    if ((this.TradeEndTime - DateTime.Now).TotalMinutes <= minutes)
                    {
                        throw new TradeFrozeException(string.Format("�������{0}���Ӳ��ܶ���", minutes));
                    }
                    SetStatus(TradeStatus.Froze);
                }
                else
                {
                    Throw(TradeStatus.Froze);
                }
            }
        }

        /// <summary>
        /// ��ֹ
        /// </summary>
        public void Terminate(int userId, string innerExplain, string outExplain)
        {
            if (this.Status != TradeStatus.Terminate)
            {
                if (this.Status == TradeStatus.Normal || this.Status == TradeStatus.Froze)
                {
                    if (userId != this.CreatorId) throw new TradeTerminateException("�㲻�Ǹ��ڵش����ߣ��޷���ֹ����");
                    var minutes = Application.GetDictionaryValue("CanFrozeOrTerminateBeforeTradeEndTime", 60);
                    if ((this.TradeEndTime - DateTime.Now).TotalMinutes <= minutes)
                    {
                        throw new TradeTerminateException(string.Format("�������{0}���Ӳ�����ֹ", minutes));
                    }
                    SetStatus(TradeStatus.Terminate);
                }
                else
                {
                    Throw(TradeStatus.Terminate);
                }
            }
        }

        private void SetStatus(TradeStatus status)
        {
            this.PrevStatus = this.Status;
            this.StatusTime = DateTime.Now;
            this.Status = status;
        }

        /// <summary>
        /// ������ȷ��
        /// </summary>
        public void ConfirmByBidder(string randomNumber, string applyNumber, int accountId, string ip, string systemInfo)
        {
            if (this.Status != TradeStatus.WaitBidderConfirm) throw new ConfirmTradeResultException("ȷ��ʧ�ܣ���ǰ״̬����ȷ");
            if (this.DealAccountId != accountId) throw new ConfirmTradeResultException("ȷ��ʧ�ܣ��㲻�Ǹ��ڵصĳɽ���");
            if (DateTime.Now > this.TradeResultConfirm.ExpiredTime) throw new ConfirmTradeResultException("ȷ��ʧ�ܣ���Ϣ�ѹ���");
            if (this.TradeResultConfirm.RandomNum != randomNumber) throw new ConfirmTradeResultException("ȷ��ʧ�ܣ�����벻��ȷ");
            SetStatus(TradeStatus.WaitHangVerify);
            this.TradeResultConfirm.ConfirmTime = DateTime.Now;
            this.TradeResultConfirm.IP = ip;
            this.TradeResultConfirm.SystemInfo = systemInfo;
        }

        /// <summary>
        /// ���������
        /// </summary>
        /// <param name="userId"></param>
        public void VerifyByHang(int userId)
        {
            if (this.Status == TradeStatus.WaitHangVerify)
            {
                if (userId != this.CreatorId) throw new VerifyTradeResultException("�㲻�ǹ��洴���ߣ��޷����");
                SetStatus(TradeStatus.Completed);
            }
            else
            {
                throw new VerifyTradeResultException("��ǰ״̬���������");
            }
        }

        public void SetReservePrice(int userId, decimal price)
        {
            if (price < this.StartPrice) throw new SetReservePriceException("�����۲��õ�����ʼ��");
            if (this.Status != TradeStatus.Normal) throw new SetReservePriceException("��ǰ״̬������¼�뱣����");
            var hours = Application.GetDictionaryValue("HowManyHoursCanSetReservePriceBeforeTradeEndTime", 1);
            if ((this.TradeEndTime - DateTime.Now).TotalHours < 1) throw new SetReservePriceException(string.Format("���׽���ǰ{0}��Сʱ������¼�뱣����", hours));
            if (!string.IsNullOrEmpty(this.ReservePrice)) throw new SetReservePriceException("���б����ۣ������ٴ�¼��");
            this.ReservePrice = price.ToString();
            this.ReservePrice = SecurityUtil.EncryptTriDes(this.ReservePrice, "12345678");
        }

        #region ����
        public void SubmitPrice(decimal price, string applyNumber, int accountId, string ip, string systemInfo)
        {
            if (this.Status != TradeStatus.Normal) throw new SubmitPriceException("��ǰ״̬����������");
            if (!(this.Stage == TradeStage.Auction || this.Stage == TradeStage.Hang)) throw new SubmitPriceException("��ǰ�׶β���������");
            if (DateTime.Now < this.TradeBeginTime || DateTime.Now > this.TradeEndTime) throw new SubmitPriceException("ֻ���ڽ����ڼ���ܱ���");
            if (this.CurrentPrice == 0 && price < this.StartPrice) throw new SubmitPriceException("�״α��۱��������ʼ��");
            var priceIncrease = GetPriceIncrease();
            if (price < this.CurrentPrice + priceIncrease) throw new SubmitPriceException("���۲��õ��ڵ�ǰ�׶α��۵���Сֵ");
            AddDetail(price, applyNumber, accountId, ip, systemInfo);
            this.CurrentPrice = price;
        }

        private void AddDetail(decimal price, string applyNumber, int accountId, string ip, string systemInfo)
        {
            this.TradeDetails.Add(new TradeDetail()
            {
                AccountId = accountId,
                ApplyNumber = applyNumber,
                CreateTime = DateTime.Now,
                Trade = this,
                PrevPrice = this.CurrentPrice,
                Price = price,
                SystemInfo = systemInfo,
                IP = ip,
                Remark = string.Format("�����ˣ�{0}����", applyNumber),
            });
        }

        private decimal GetPriceIncrease()
        {
            if (this.Stage == TradeStage.Hang)
            {
                return this.HangPriceIncrease;
            }
            return this.AuctionPriceIncrease;
        } 
        #endregion

        private void Throw(TradeStatus status)
        {
            string originalDesc = Tlw.ZPG.Infrastructure.Utils.EnumUtil.GetDescription(this.Status);
            string targetDesc = Tlw.ZPG.Infrastructure.Utils.EnumUtil.GetDescription(status);
            throw new DomainException(string.Format("����������״̬��ԭ״̬��{0}��Ŀ��״̬��{1}", originalDesc, targetDesc));
        }
    }
}