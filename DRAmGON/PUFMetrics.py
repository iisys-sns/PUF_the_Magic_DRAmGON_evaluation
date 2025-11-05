#!/usr/bin/env python3

"""
getJaccardIndex calculates the jaccard index between two lists list1 and list2.
"""
def getJaccardIndex(set1, set2):
	nUnion = len(set1.union(set2))
	nIntersect = len(set1.intersection(set2))

	if nUnion == 0:
		return float('inf')

	return nIntersect / nUnion

"""
getFractionalHammingDistance returns the fractional hamming distance of a list
"""
def getFractionalHammingDistance(response1, response2, itemSize):
	return (response1 ^ response2).bit_count() / itemSize

"""
getJaccardIndexMulti calculates the jaccard index between multiple lists by
calculating the pairwise jaccard index for each possible pair of lists and
returning the average.
"""
def getJaccardIndexMulti(lists):
	nJaccard = 0
	sumJaccard = 0
	for idx in range(0, len(lists)):
		for i in range(idx, len(lists)):
			# TODO: Skipping empty sets, e.g., measurements in which not a single
			# considered flip occurred. Not sure if that is the way it is done
			if len(lists[idx]) == 0 and len(lists[i]) == 0:
				continue
			sumJaccard += getJaccardIndex(lists[idx], lists[i])
			nJaccard += 1

	if nJaccard == 0:
		return float('inf')

	return sumJaccard / nJaccard

"""
getJaccardIndexMultiTwoLists calculates the jaccard index between two lists
which contain multiple lists each. It calculates the jaccard index between
each list of lists1 and each list of lists2. The average is returned.
"""
def getJaccardIndexMultiTwoLists(lists1, lists2):
	nJaccard = 0
	sumJaccard = 0
	for idx in range(0, len(lists1)):
		for i in range(0, len(lists2)):
			# TODO: Skipping empty sets, e.g., measurements in which not a single
			# considered flip occurred. Not sure if that is the way it is done
			if len(lists1[idx]) == 0 and len(lists2[i]) == 0:
				continue
			sumJaccard += getJaccardIndex(lists1[idx], lists2[i])
			nJaccard += 1

	if nJaccard == 0:
		return float('inf')

	return sumJaccard / nJaccard

"""
getAverageHammingDistance takes a list of lists (containing single PUF responses)
and calculates the average hamming distance of all items in the sub-lists.
Additionally, it takes the itemSize to consider how big (in bits) a single
response is.
"""
def getAverageIntraHammingDistance(list1, key, itemSize):
	nHammingDistances = 0
	sumHammingDistances = 0
	for idx1 in range(0, len(list1)):
		#for idx2 in range(idx1 + 1, len(list1)):
		#	sumHammingDistances += getFractionalHammingDistance(list1[idx1], list1[idx2], itemSize)
		#	nHammingDistances += 1
		sumHammingDistances += getFractionalHammingDistance(list1[idx1], key, itemSize)
		nHammingDistances += 1
			#print("[DEBUG]: Measured fractional HD between indices" + str(idx1) + " and " + str(idx2) + ": " + str(getFractionalHammingDistance(list1[idx1], list1[idx2], itemSize)))

	return sumHammingDistances / nHammingDistances

def getAverageInterHammingDistance(list1, list2, itemSize):
	nHammingDistances = 0
	sumHammingDistances = 0
	for idx1 in range(0, len(list1)):
		for idx2 in range(0, len(list2)):
			#print("[DEBUG]: Comparing " + str(bin(list1[idx1])) + " and " + str(bin(list2[idx2])) + " with HD: " + str(getFractionalHammingDistance(list1[idx1], list2[idx2], itemSize)))
			sumHammingDistances += getFractionalHammingDistance(list1[idx1], list2[idx2], itemSize)
			nHammingDistances += 1

	return sumHammingDistances / nHammingDistances

def getUniquenessMatrix(pufs, nBits):
	uniqueness = {}
	for firstSystemName in pufs:

		if pufs[firstSystemName].isInvalid():
			continue

		uniqueness[firstSystemName] = {}

		for secondSystemName in pufs:

			if pufs[secondSystemName].isInvalid():
				continue

			#print("\x1b[90m[DEBUG]: Calculating uniqueness for systems " + firstSystemName + " <-> " + secondSystemName + "\x1b[0m")
			secondResponses = []

			for measurement in pufs[secondSystemName]._fastMeasurements:
				secondResponses.append(pufs[firstSystemName].getPUFResponse(measurement))
			#uniqueness[firstSystemName][secondSystemName] = getAverageInterHammingDistance(pufs[firstSystemName].getAllResponses(), secondResponses, nBits)
			uniqueness[firstSystemName][secondSystemName] = getAverageIntraHammingDistance(secondResponses, pufs[firstSystemName]._binaryKey, nBits)

	return uniqueness

def getReliabilityList(pufs, nBits):
	reliability = {}
	for systemName in pufs:
		if pufs[systemName].isInvalid():
			continue
		#print("\x1b[90m[DEBUG]: Calculating reliability for system " + systemName + "\x1b[0m")
		reliability[systemName] = getAverageIntraHammingDistance(pufs[systemName].getAllResponses(), pufs[systemName]._binaryKey, nBits)

	return reliability

def getUniformityList(systems, itemSize):
	uniformity = {}
	for systemName in systems:
		uniformity[systemName] = getAverageHammingDistance(systems[systemName], itemSize)

	return uniformity

def getBitAliasingList(systems):
	print("Not implemented yet!")
	os._exit(1)

def getMinEntropy(systems):
	print("Not implemented yet!")
	os._exit(1)
