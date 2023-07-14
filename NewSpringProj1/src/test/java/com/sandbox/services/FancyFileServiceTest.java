package com.sandbox.services;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;

import java.io.File;

import static org.hamcrest.MatcherAssert.assertThat;
import static org.hamcrest.Matchers.*;
import static org.mockito.Mockito.doReturn;

class FancyFileServiceTest {

    private FancyFileService service;

    @BeforeEach
    void setUp() {
        service = new FancyFileService();
    }

    @AfterEach
    void tearDown() {
        service = null;
    }

    @Test
    void getRootFolderFiles() {
        FancyFileService service1 = Mockito.spy(service);

        var roots = new File[]{new File("C:\\")};
        var files = new File[]{new File("C:\\Hello.csv", "C:\\Folder")};
        doReturn(roots).when(service1).listRoots();
        doReturn(files).when(service1).listFiles(roots[0]);

        File[] testSub = service1.getRootFolderFiles("C");
        assertThat(testSub, is(files));
    }

    @Test
    void listRoots() {
        var root = new File("C:\\");
        assertThat(new FancyFileService().listRoots(), hasItemInArray(root));
    }

    @Test
    void listFiles() {
        FancyFileService service1 = Mockito.spy(service);
        var parentFolder = new File("C:\\");
        var files = new File[]{new File("C:\\Hello.csv", "C:\\Folder")};
        doReturn(files).when(service1).listFiles(parentFolder);
        File[] testSub = service1.listFiles(parentFolder);
        assertThat(testSub, is(files));
    }
}