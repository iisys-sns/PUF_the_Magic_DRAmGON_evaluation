#!/usr/bin/env python3
from abc import ABC, abstractmethod
import random
import getAddress

class PUF(ABC):
	def __init__(self, measurements, nMeasurementsUsedForConstructions, nResponseBits, baseOffset, bankFunctions, rowFunction):
		if nMeasurementsUsedForConstructions < 1:
			nMeasurementsUsedForConstructions = int(len(measurements) * nMeasurementsUsedForConstructions)
		self._constructionMeasurements = measurements[0:nMeasurementsUsedForConstructions]
		self._verificationMeasurements = measurements[nMeasurementsUsedForConstructions:len(measurements)]
		self._fastMeasurements = []
		self._nResponseBits = nResponseBits
		self._invalid = False
		self._baseOffset = 0
		self._bank = measurements[0][0]["dram_addr"]["bank"]
		self._bankFunctions = bankFunctions
		self._bankFunctionsStr = ""
		self._rowFunction = rowFunction
		self._rowFunctionStr = ""
		self._allResponses = ""
		self._DramFunctions = getAddress.getDRAMFunctions(self._rowFunction, self._bankFunctions)
		self._setBaseOffset(baseOffset)
		self._constructPUF()

		for measurement in self._constructionMeasurements + self._verificationMeasurements:
			fastMeasurement = {}
			for flip in measurement:
				absoluteOffset = flip["dram_addr"]["row"] * 8192 + flip["dram_addr"]["col"]
				offset = absoluteOffset - self._baseOffset
				fastMeasurement[offset] = flip
			self._fastMeasurements.append(fastMeasurement)
			

	@abstractmethod
	def _constructPUF(self):
		pass

	@abstractmethod
	def getPUFResponse(self, measurement):
		pass

	def _setBaseOffset(self, baseOffset):
		result = self._DramFunctions.getBankRowColumn(baseOffset)
		self._baseOffset = result[1] * 8192 + result[2]
		

	def getMeasurementsForConstruction(self):
		return len(self._constructionMeasurements)

	def getMeasurementsForVerification(self):
		return len(self._verificationMeasurements)

	def getPUFResponses(self, maxResponses=-1):
		responses = []
		for verificationMeasurement in self._verificationMeasurements:
			responses.append(self.getPUFResponse(verificationMeasurement))
			if len(responses) == maxResponses:
				break

		return responses

	def isInvalid(self):
		return self._invalid

		return responses

	def getResponseBits(self):
		return self.nResponseBits

	def getBankFunctionStr(self):
		if self._bankFunctionsStr != "":
			return self._bankFunctionsStr

		for bankFunction in self._bankFunctions:
			if self._bankFunctionsStr != "":
				self._bankFunctionsStr += ","
			self._bankFunctionsStr += str(hex(bankFunction))

		return self._bankFunctionsStr

	def getRowFunctionStr(self):
		if self._rowFunctionStr != "":
			return self._rowFunctionStr

		self._rowFunctionStr = str(hex(self._rowFunction))
		return self._rowFunctionStr

	@abstractmethod
	def getRawAttributes(self):
		pass

class BinaryFlipEncodingPUF(PUF):
	def __init__(self, measurements, nMeasurementsUsedForConstructions, nResponseBits, baseOffset, bankFunctions, rowFunction):
		self._offsets = []
		self._stableOffsets = {}
		self._key = []
		self._binaryKey = 0
		super().__init__(measurements, nMeasurementsUsedForConstructions, nResponseBits, baseOffset, bankFunctions, rowFunction)

	def _addZeroAddressesTrivial(self, oneOffsets, minOffset, maxOffset):
		oneOffsets.sort()

		zeroOffsets = []
		lastOffset = minOffset
		for offset in oneOffsets:
			if offset - lastOffset > 2:
				valid = False
				while not valid:
					trialOffset = random.randint(lastOffset, offset)
					if trialOffset not in self._stableOffsets:
						valid = True
				zeroOffsets.append(trialOffset)
				lastOffset = offset

		if maxOffset - lastOffset > 2:
			zeroOffsets.append(random.randint(lastOffset, maxOffset))

		while len(zeroOffsets) < self._nResponseBits - len(oneOffsets):
			valid = False
			while not valid:
				trialOffset = random.randint(minOffset, maxOffset)
				if trialOffset not in self._stableOffsets:
					valid = True
			zeroOffsets.append(trialOffset)

		random.shuffle(oneOffsets)
		random.shuffle(zeroOffsets)

		self._offsets = []
		zIdx = 0
		oIdx = 0
		for i in range(len(self._key)):
			if self._key[i] == 0:
				self._offsets.append(zeroOffsets[zIdx])
				zIdx += 1
			else:
				self._offsets.append(oneOffsets[oIdx])
				oIdx += 1

	def _addZeroAddressesMontecarlo(self, oneOffsets, minOffset, maxOffset):
		oneOffsets.sort()
		zeroOffsets = []

		offsetProbabilities = []
		lastOffset = minOffset
		minWidth = -1
		widthSum = 0
		for oneOffset in oneOffsets:
			width = oneOffset - lastOffset
			widthSum += width
			if minWidth == -1 or width < minWidth:
				minWidth = width

			offsetProbabilities.append({"min": lastOffset, "max": oneOffset, "width": width})
			lastOffset = oneOffset

		width = maxOffset - lastOffset
		widthSum += width
		offsetProbabilities.append({"min": lastOffset, "max": maxOffset, "width": width})

		if minWidth == -1 or width < minWidth:
			minWidth = width

		widthAvg = int(widthSum / len(offsetProbabilities))

		offsetProbabilities.append({"min": maxOffset, "max": maxOffset + widthAvg, "width": widthAvg})
		offsetProbabilities.insert(0,{"min": minOffset - widthAvg, "max": minOffset, "width": widthAvg})

		# Specify min bin width. All smaller bins are considered to be this size (increases speed)
		if minWidth <= 10:
			minWidth = 10
		for offsetProbability in offsetProbabilities:
			if offsetProbability["width"] == 0:
				offsetProbability["prob"] = 0
			else:
				offsetProbability["prob"] = minWidth / offsetProbability["width"]

		while len(zeroOffsets) < self._nResponseBits - len(oneOffsets):
			randomOffset = random.randint(minOffset, maxOffset)
			if randomOffset in self._stableOffsets:
				continue
			randomYValue = random.random()

			for offsetProbability in offsetProbabilities:
				if randomOffset < offsetProbability["min"] or randomOffset >= offsetProbability["max"]:
					continue
				if randomYValue <= offsetProbability["prob"]:
					zeroOffsets.append(randomOffset)
					break

		fullList = oneOffsets + zeroOffsets
		fullList.sort()
		binary = ""
		for offset in fullList:
			if offset in oneOffsets:
				binary += "1"
			else:
				binary += "0"
		print(binary)
		with open("addressOffsets.csv", "a") as f:
			f.write(binary + "\n")

		random.shuffle(oneOffsets)
		random.shuffle(zeroOffsets)

		self._offsets = []
		zIdx = 0
		oIdx = 0
		for i in range(len(self._key)):
			if self._key[i] == 0:
				self._offsets.append(zeroOffsets[zIdx])
				zIdx += 1
			else:
				self._offsets.append(oneOffsets[oIdx])
				oIdx += 1

	def _constructPUF(self):
		self._stableAddresses = {}
		minOffset = -1
		maxOffset = -1

		for measurement in self._constructionMeasurements:
			seenInThisRun = set()
			for flip in measurement:
				absoluteOffset = flip["dram_addr"]["row"] * 8192 + flip["dram_addr"]["col"]
				offset = absoluteOffset - self._baseOffset

				if offset in seenInThisRun:
					continue
				seenInThisRun.add(offset)

				if offset not in self._stableOffsets:
					self._stableOffsets[offset] = 1
				else:
					self._stableOffsets[offset] += 1

				if minOffset == -1 or offset < minOffset:
					minOffset = offset

				if maxOffset == -1 or offset > maxOffset:
					maxOffset = offset

		self._stableOffsets = dict(sorted(self._stableOffsets.items(), key=lambda item: item[1], reverse = True))

		self._key = []
		nOnes = 0
		for i in range(0, self._nResponseBits):
			self._key.append(random.randint(0, 1))
			self._binaryKey *= 2
			if self._key[i] == 1:
				self._binaryKey += 1
				nOnes += 1

		i = 0
		oneOffsets = []
		for offset in self._stableOffsets:
			if i == nOnes:
				break
			oneOffsets.append(offset)
			#print("Using offset " + str(offset) + " which occurred " + str(self._stableOffsets[offset]) + " of " + str(len(self._constructionMeasurements)) + " times.")
			i += 1

		if i < nOnes:
			self._invalid = True
			print("PUF is invalid!")
		else:
			self._addZeroAddressesMontecarlo(oneOffsets, minOffset, maxOffset)

		# Debugging
		#keyStr = ""
		#for bit in self._key:
		#	keyStr += str(bit)
		#print("Generated Key: " + keyStr)
		#for measurement in self._constructionMeasurements:
		#	key = self.getPUFResponse(measurement)
		#	print(bin(key))
		#os._exit(0)

	def getPUFResponse(self, measurement):
		key = 0
		for i in range(0, self._nResponseBits):
			key *= 2
			#absoluteOffset = self._offsets[i] + self._baseOffset
			#column = absoluteOffset % 8192
			#row = absoluteOffset // 8192
			#for flip in measurement:
			#	if flip["dram_addr"]["row"] == row and flip["dram_addr"]["col"] == column:
			#		key += 1
			if self._offsets[i] in measurement:
				key += 1

		return key

	def getAllResponses(self):
		if len(self._allResponses) > 0:
			return self._allResponses

		responses = []
		for measurement in self._fastMeasurements:
			responses.append(self.getPUFResponse(measurement))
			#print("[DEBUG: Added response: " + str(bin(responses[len(responses)-1])))

		self._allResponses = responses
		return responses

	def getOffsetAddresses(self):
		addresses = []
		for offset in self._offsets:
			absoluteOffset = offset + self._baseOffset
			column = absoluteOffset % 8192
			row = absoluteOffset // 8192
			addresses.append(self._DramFunctions.getAddr(row, self._bank, column))
		return addresses

	def getKey(self):
		return self._key

	def getRawAttributes(self):
		measurements = []
		for measurement in self._constructionMeasurements + self._verificationMeasurements:
			offsets = set()
			for flip in measurement:
				absoluteOffset = flip["dram_addr"]["row"] * 8192 + flip["dram_addr"]["col"]
				offset = absoluteOffset - self._baseOffset
				if offset in self._stableOffsets and self._stableOffsets[offset] >= 10:
					offsets.add(offset)
			measurements.append(offsets)

		return measurements
