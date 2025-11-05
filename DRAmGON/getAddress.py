import sys
import os
import numpy as np
import pprint as pp

class BinInt(int):
	def __repr__(s):
		return s.__str__()

	def __str__(s):
		return f"{s:#032b}"

class DRAMFunctions():
	def __init__(self, base_offset, bank_fns, row_fn, col_fn):
		def to_binary_array(v):
			vals = []
			for x in range(30):
				if (v >> x) & 1:
					vals.append(1 << x)
			return list(reversed(vals))

		def gen_mask(v):
			len_mask = bin(v).count("1")
			mask = (1 << len_mask)-1
			return (len_mask, mask)

		bank_mask = (1 << len(bank_fns))-1
		row_arr = to_binary_array(row_fn)
		len_row_mask, row_mask = gen_mask(row_fn)
		col_arr = to_binary_array(col_fn)
		len_col_mask, col_mask = gen_mask(col_fn)

		self.base_offset = base_offset
		self.row_arr = row_arr
		self.col_arr = col_arr
		self.bank_arr = bank_fns
		self.row_shift = 0
		self.col_shift = len_row_mask
		self.bank_shift = len_row_mask + len_col_mask
		self.row_mask = BinInt(row_mask)
		self.col_mask = BinInt(col_mask)
		self.bank_mask = BinInt(bank_mask)
		self.dram_mtx = self.to_dram_mtx()
		self.addr_mtx = self.to_addr_mtx()

	def to_dram_mtx(self):
		mtx = self.bank_arr + self.col_arr + self.row_arr
		return list(map(lambda v: BinInt(v), mtx))

	def to_addr_mtx(self):
		dram_mtx = self.to_dram_mtx()
		mtx = np.array([list(map(int, list(f"{x:030b}"))) for x in dram_mtx])
		assert mtx.shape == (30, 30)
		inv_mtx = list(map(abs, np.linalg.inv(mtx).astype('int64')))
		inv_arr = []
		for i in range(len(inv_mtx)):
			inv_arr.append(BinInt("0b" + "".join(map(str, inv_mtx[i])), 2))
		return inv_arr

	def getAddr(self, row, bank, column):
		linearized = bank * 2**self.bank_shift | row * 2**self.row_shift | column * 2**self.col_shift
		virt = 0
		for mask in self.addr_mtx:
			virt *= 2
			virt |= (linearized & mask).bit_count() % 2
		return self.base_offset | virt

	def getBankRowColumn(self, address):
		if address & self.base_offset != 0:
			address -= self.base_offset

		result = 0
		for mask in self.dram_mtx:
			result *= 2
			result |= (address & mask).bit_count() % 2

		bank = (result // 2**self.bank_shift) & self.bank_mask
		row = (result // 2**self.row_shift) & self.row_mask
		col = (result // 2**self.col_shift) & self.col_mask

		return [bank, row, col]

def getDRAMFunctions(rowMask, dramFunctions):
	columnMask = 0x1fff
	functions = DRAMFunctions(0x2000000000, dramFunctions, rowMask, columnMask)
	return functions
