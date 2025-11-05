#!/usr/bin/env python3
import sys
import os
import json
import scipy
from scipy.stats import mannwhitneyu
from statsmodels.stats.multitest import multipletests

import PUF
import PUFMetrics

def getColorStr(number):
	colorCode = "196"

	if number <= 0.1 or number >= 0.9:
		colorCode = "34"
	elif number <= 0.2 or number >= 0.8:
		colorCode = "70"
	elif number <= 0.3 or number >= 0.7:
		colorCode = "106"
	elif number <= 0.4 or number >= 0.6:
		colorCode = "142"
	elif number <= 0.45 or number >= 0.55:
		colorCode = "178"

	numberStr = "%.3f" % (number)
	return "\x1b[38:5:" + colorCode + "m" + numberStr + "\x1b[0m"

def getRowFunction(bankFunctions):
	if len(bankFunctions) == 4:
		return 0x3ffe0000
	if len(bankFunctions) == 5:
		return 0x3ffc0000
	if len(bankFunctions) == 6:
		return 0x3ff80000
	print("\x1b[31mInvalid number of bank functions (" + len(bankFunctions) + "). Should be 4, 5, or 6.\x1b[0m")

def loadFile(path):
	with open(path) as f:
		content = f.read()
	json_data = json.loads(content)["sweeps"]
	if len(json_data) != 1:
		print("\x1b[31mFile " + path + " has " + len(json_data) + " sweeping runs (should be 1).\x1b[0m")
		os._exit(255)

	return json_data[0]["flips"]["details"]

def loadOffset(path):
	with open(path) as f:
		content = f.read()
	return int(content.split("\n")[0].split(" ")[6], 16)

def loadBankFunctions(path):
	with open(path) as f:
		content = f.read()
	content = content.replace("[", "")
	content = content.replace("]", "")
	functions = []
	for function in content.split(", "):
		functions.append (int(function))
	return functions
	

def loadSystem(basePath, roomName, systemName):
	print("\x1b[90m[DEBUG]: Loading from base path " + basePath + " and room " + roomName + " and system " + systemName + "\x1b[0m")
	measurements = []

	systemPath = basePath + "/" + roomName + "/" + systemName

	offset = ""
	bankFunctions = []
	for subdir, dirs, files in os.walk(systemPath):
		for file in files:
			path = subdir + "/" + file
			if file == "sweep-summary-1x256MB.json":
				results = loadFile(path)
				if len(results) >= 1:
					measurements.append(results)
			if file == "offsets.txt":
				newOffset = loadOffset(path)
				if offset == "":
					offset = newOffset
				if offset != newOffset:
					print("\x1b[31mOffset for sweeping run (" + newOffset + ") is different than for other sweeping runs (" + offset + ").\x1b[0m")
			if file == "injected-functions.txt":
				bankFunctions = loadBankFunctions(path)

	return [measurements, offset, bankFunctions]

def loadRoom(basePath, roomName):
	print("\x1b[90m[DEBUG]: Loading from base path " + basePath + " and room " + roomName + "\x1b[0m")
	data = {}
	offsets = {}
	bankFunctions = {}
	for f in os.scandir(basePath + "/" + roomName):
		if not f.is_dir():
			continue
		results = loadSystem(basePath, roomName, f.name)
		measurements = results[0]
		if len(measurements) == 0:
			continue
		offset = results[1]
		bankFunction = results[2]

		data[roomName + "-" + f.name] = measurements
		offsets[roomName + "-" + f.name] = offset
		bankFunctions[roomName + "-" + f.name] = bankFunction

		#return [data, offsets, bankFunctions]

	return [data, offsets, bankFunctions]

def loadAll(basePath):
	print("\x1b[90m[DEBUG]: Loading from base path " + basePath + "\x1b[0m")
	data = {}
	offsets = {}
	bankFunctions = {}
	for f in os.scandir(basePath):
		if not f.is_dir():
			continue
		if f.name == "evaluation" or f.name == "results" or f.name == "utils":
			continue
		results = loadRoom(basePath, f.name)
		data = data | results[0]
		offsets = offsets | results[1]
		bankFunctions = bankFunctions | results[2]

		#if len(data) == 2:
		#	return [data, offsets, bankFunctions]

	return [data, offsets, bankFunctions]

def constructPUFs(data, offsets, bankFunctions, nMeasurementsUsedForConstructions, nResponseBits):
	pufs = {}
	for systemId in data:
		print("\x1b[90m[DEBUG]: Constructing PUF for system " + systemId + "\x1b[0m")
		puf = PUF.BinaryFlipEncodingPUF(data[systemId], nMeasurementsUsedForConstructions, nResponseBits, offsets[systemId], bankFunctions[systemId], getRowFunction(bankFunctions[systemId]))
		if not puf.isInvalid():
			pufs[systemId] = puf
	return pufs

def evaluate(pufs, nBits):
	# Perform evaluations
	##########################################

	# Mann-Whitney U test 
	uTestPValues = []
	for systemId in pufs:
		if pufs[systemId].isInvalid():
			continue

		offsetAddresses = pufs[systemId].getOffsetAddresses()
		key = pufs[systemId].getKey()
		batch0 = []
		batch1 = []
		for idx in range(len(key)):
			if key[idx] == 0:
				batch0.append(offsetAddresses[idx])
			else:
				batch1.append(offsetAddresses[idx])

		stat, p_value = mannwhitneyu(batch0, batch1)
		uTestPValues.append(p_value)

	reject = multipletests(uTestPValues, alpha=0.05, method="holm")
	invalid = False
	for entry in reject[0]:
		invalid = invalid or entry

	# Reliability
	reliabilityList = PUFMetrics.getReliabilityList(pufs, nBits)

	# Uniqueness
	uniquenessMatrix = PUFMetrics.getUniquenessMatrix(pufs, nBits)

	# Print results
	##########################################

	# U-Test p Values
	uTestExportStr = ""
	systemIdx = 1
	for p_value in uTestPValues:
		uTestExportStr += str(systemIdx) + "," + str(p_value) + "\n"
		if p_value >= 0.05:
			print("\x1b[32m[SAME]: p-Value of " + systemId + ":  " + str(p_value) + "\x1b[0m")
		else:
			print("\x1b[31m[DIFF]: p-Value of " + systemId + ":  " + str(p_value) + "\x1b[0m")
		systemIdx += 1
	if invalid:
		print("\x1b[31mDistributions of addresses encoding ones and zeroes do not match.\x1b[0m")
	else:
		print("\x1b[32mDistributions of addresses encoding ones and zeroes match.\x1b[0m")

	# Reliability
	reliabilityExportStr = ""
	systemIdx = 1
	for systemId in reliabilityList:
		reliabilityExportStr += str(systemIdx) + "," + str(reliabilityList[systemId]) + "\n"
		if reliabilityList[systemId] <= 0.05:
			print("\x1b[32mAverage Fractional HD: " + systemId + ": " + str(reliabilityList[systemId]) + "\x1b[0m")
		elif reliabilityList[systemId] <= 0.2:
			print("\x1b[33mAverage Fractional HD: " + systemId + ": " + str(reliabilityList[systemId]) + "\x1b[0m")
		else:
			print("\x1b[31mAverage Fractional HD: " + systemId + ": " + str(reliabilityList[systemId]) + "\x1b[0m")
		systemIdx += 1

	# Uniqueness
	uniquenessExportStr = ""

	system1Idx = 1
	for systemId1 in uniquenessMatrix:
		line = ""
		system2Idx = 1
		for systemId2 in uniquenessMatrix[systemId1]:
			uniquenessExportStr += str(system1Idx) + "," + str(system2Idx) + "," + str(uniquenessMatrix[systemId1][systemId2]) + "\n"
			line += getColorStr(uniquenessMatrix[systemId1][systemId2]) + " "
			system2Idx += 1
		system1Idx += 1
		print(line)

	with open("encodingUTest.csv", "w") as f:
		f.write(uTestExportStr)

	with open("encodingReliability.csv", "w") as f:
		f.write(reliabilityExportStr)

	with open("encodingUniqueness.csv", "w") as f:
		f.write(uniquenessExportStr)


if len(sys.argv) != 4:
	print("Usage: " + sys.argv[0] + " <path/to/experiment/base> <nMeasurementsUsedForConstructions or percentage when < 1 > <nResponseBits>")
	os._exit(255)

baseDir = sys.argv[1]
nMeasurementsUsedForConstructions = float(sys.argv[2])
if nMeasurementsUsedForConstructions >= 1:
	nMeasurementsUsedForConstructions = int(sys.argv[2])
nResponseBits = int(sys.argv[3])

results = loadAll(sys.argv[1])
data = results[0]
offsets = results[1]
print("Offsets: " + str(offsets))
bankFunctions = results[2]
pufs = constructPUFs(data, offsets, bankFunctions, nMeasurementsUsedForConstructions, nResponseBits)
evaluate(pufs, nResponseBits)
