package com.sandbox.controllers;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.sandbox.services.FileService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;

import java.io.File;
import java.util.Arrays;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.result.MockMvcResultHandlers.print;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

@WebMvcTest(controllers = FileController.class)
public class FileControllerTest {

    @Autowired
    private MockMvc mvc;
    @Autowired
    private ObjectMapper objectMapper;

    @MockBean
    private FileService service;

    @Test
    public void getRootAll_DiskLetterC_Ok() throws Exception {
        var expectedBody = new File[]{new File("C:\\TestFile.csv")};
        when(service.getRootFolderFiles("C"))
                .thenAnswer(in -> expectedBody);
        var mvcResult = mvc
                .perform(MockMvcRequestBuilders
                        .get("/files/root-all")
                        .param("disk-letter", "C"))
                .andDo(print())
                .andExpect(status().isOk())
                .andReturn();
        var body = mvcResult.getResponse().getContentAsString();
        assertThat(body)
                .isEqualToIgnoringWhitespace(objectMapper.writeValueAsString(Arrays.stream(expectedBody).toList()));
    }

    @Test
    public void getRootAll_NoDiskLetter_BadRequest() throws Exception {
        mvc.perform(MockMvcRequestBuilders
                        .get("/files/root-all"))
                .andDo(print())
                .andExpect(status().isBadRequest())
                .andReturn();
        mvc.perform(MockMvcRequestBuilders
                        .get("/files/root-all")
                        .param("disk-letter", ""))
                .andDo(print())
                .andExpect(status().isBadRequest())
                .andReturn();
    }
}