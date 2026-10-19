using System;
using System.Collections.Generic;
using System.Linq;

namespace LConnectCore.Products.TLFan;

public class TLFanLightingGroupManager
{
	public class GroupConfiguration
	{
		private readonly int[] order;

		private readonly int[] groupQuantities;

		private int actualTotalQuantity;

		public int MaxFanQuantityPerGroup { get; private set; }

		public int MaxGroupQuantity { get; private set; }

		public int[] Order
		{
			get
			{
				return (int[])order.Clone();
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException();
				}
				if (value.Length != MaxGroupQuantity)
				{
					throw new ArgumentException();
				}
				HashSet<int> hashSet = new HashSet<int>();
				foreach (int num in value)
				{
					if (num < 0 || num >= MaxGroupQuantity)
					{
						throw new ArgumentException();
					}
					hashSet.Add(num);
				}
				if (MaxGroupQuantity != hashSet.Count)
				{
					throw new ArgumentException();
				}
				Array.Copy(value, order, order.Length);
			}
		}

		public int[] GroupQuantities
		{
			get
			{
				return (int[])groupQuantities.Clone();
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException();
				}
				if (value.Length != MaxGroupQuantity)
				{
					throw new ArgumentException();
				}
				foreach (int num in value)
				{
					if (num < 0 || num > MaxFanQuantityPerGroup)
					{
						throw new ArgumentException();
					}
				}
				Array.Copy(value, groupQuantities, groupQuantities.Length);
			}
		}

		public int TotalQuantity => groupQuantities.Sum();

		public int ActualTotalQuantity
		{
			get
			{
				return actualTotalQuantity;
			}
			set
			{
				if (value < 0)
				{
					throw new ArgumentException();
				}
				actualTotalQuantity = value;
			}
		}

		public GroupConfiguration(int maxGroupQuantity, int maxFanQuantityPerGroup)
		{
			if (maxGroupQuantity <= 0 || maxFanQuantityPerGroup <= 0)
			{
				throw new ArgumentException();
			}
			MaxGroupQuantity = maxGroupQuantity;
			MaxFanQuantityPerGroup = maxFanQuantityPerGroup;
			order = new int[maxGroupQuantity];
			for (int i = 0; i < order.Length; i++)
			{
				order[i] = i;
			}
			groupQuantities = new int[maxGroupQuantity];
		}

		public bool Adjust()
		{
			int num = TotalQuantity - ActualTotalQuantity;
			if (num <= 0)
			{
				return false;
			}
			decreaseQuantity(num);
			return true;
		}

		public bool UpdateActualTotalQuantity(int actualTotalQuantity)
		{
			int num = actualTotalQuantity - ActualTotalQuantity;
			ActualTotalQuantity = actualTotalQuantity;
			if (num == 0)
			{
				return false;
			}
			if (num > 0)
			{
				increaseQuantity(num);
			}
			else
			{
				decreaseQuantity(-num);
			}
			return true;
		}

		private void increaseQuantity(int quantity)
		{
			for (int num = order.Length - 1; num >= 0; num--)
			{
				int num2 = order[num];
				if (groupQuantities[num2] != 0 && groupQuantities[num2] != MaxFanQuantityPerGroup)
				{
					quantity = increaseQuantity(num2, quantity);
					if (quantity <= 0)
					{
						return;
					}
				}
			}
			for (int i = 0; i < order.Length; i++)
			{
				int num3 = order[i];
				if (groupQuantities[num3] == 0)
				{
					quantity = increaseQuantity(num3, quantity);
					if (quantity <= 0)
					{
						break;
					}
				}
			}
		}

		private int increaseQuantity(int groupIndex, int quantity)
		{
			groupQuantities[groupIndex] += quantity;
			quantity = 0;
			if (groupQuantities[groupIndex] > MaxFanQuantityPerGroup)
			{
				quantity = groupQuantities[groupIndex] - MaxFanQuantityPerGroup;
				groupQuantities[groupIndex] = MaxFanQuantityPerGroup;
			}
			return quantity;
		}

		private void decreaseQuantity(int quantity)
		{
			for (int num = order.Length - 1; num >= 0; num--)
			{
				int num2 = order[num];
				if (groupQuantities[num2] != 0)
				{
					quantity = decreaseQuantity(num2, quantity);
					if (quantity <= 0)
					{
						break;
					}
				}
			}
		}

		private int decreaseQuantity(int groupIndex, int quantity)
		{
			groupQuantities[groupIndex] -= quantity;
			quantity = 0;
			if (groupQuantities[groupIndex] < 0)
			{
				quantity -= groupQuantities[groupIndex];
				groupQuantities[groupIndex] = 0;
			}
			return quantity;
		}
	}

	public const int MaxPortQuantity = 4;

	private readonly int[] order = new int[4];

	private readonly PortType[] portTypes = new PortType[4];

	private readonly Dictionary<PortType, GroupConfiguration>[] groupConfigurations = new Dictionary<PortType, GroupConfiguration>[4];

	public int[] Order
	{
		get
		{
			return (int[])order.Clone();
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException();
			}
			if (value.Length != 4)
			{
				throw new ArgumentException();
			}
			HashSet<int> hashSet = new HashSet<int>();
			foreach (int num in value)
			{
				if (num < 0 || num >= 4)
				{
					throw new ArgumentException();
				}
				hashSet.Add(num);
			}
			if (4 != hashSet.Count)
			{
				throw new ArgumentException();
			}
			Array.Copy(value, order, order.Length);
		}
	}

	public PortType[] PortTypes => (PortType[])portTypes.Clone();

	public GroupConfiguration this[int port]
	{
		get
		{
			if (portTypes[port] == PortType.None)
			{
				return null;
			}
			return groupConfigurations[port][portTypes[port]];
		}
	}

	public GroupConfiguration this[int port, PortType portType] => groupConfigurations[port][portType];

	public TLFanLightingGroupManager()
	{
		for (int i = 0; i < groupConfigurations.Length; i++)
		{
			groupConfigurations[i] = new Dictionary<PortType, GroupConfiguration>
			{
				[PortType.LED] = new GroupConfiguration(4, 4),
				[PortType.LCD] = new GroupConfiguration(1, 3)
			};
		}
	}

	public bool ChangePortType(int port, PortType portType)
	{
		bool result = portTypes[port] != portType;
		portTypes[port] = portType;
		return result;
	}
}
